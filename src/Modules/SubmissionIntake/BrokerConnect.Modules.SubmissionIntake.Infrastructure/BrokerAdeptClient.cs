using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace BrokerConnect.Modules.SubmissionIntake.Infrastructure;

// IR-001: ACORD/ADEPT broker exchange, owned here as a dedicated anti-corruption-layer
// HTTP client (Solution Arch §4.3, ADR-008). design.md Interfaces.
public interface IBrokerAdeptClient
{
    Task<AdeptNormalizationResult> NormalizeAsync(string rawPayloadRef, CancellationToken cancellationToken);
}

public sealed record AdeptNormalizationResult(
    bool Succeeded, string? ClassOfBusiness, string? Territory, decimal? LineSizeSought,
    string? KeyTerms, string? NamedInsured, DateOnly? EffectiveDateRequested,
    string? FailureReason);

// ADR-008: retry with jittered backoff + circuit breaker; breaker-open state is logged/
// alertable, never silently retried forever. On exhausted retries / open breaker, this
// returns a failed AdeptNormalizationResult with a synthetic "AdeptServiceUnavailable"
// FailureReason rather than throwing -- NormalizeSubmissionViaAdeptHandler (4.5) turns
// that into SubmissionNormalizationFailed instead of leaving the submission stuck-pending
// (design.md Edge Cases: "Submission stuck in normalization").
public sealed class BrokerAdeptClient : IBrokerAdeptClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BrokerAdeptClient> _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;

    public BrokerAdeptClient(HttpClient httpClient, ILogger<BrokerAdeptClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _resiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(response => !response.IsSuccessStatusCode)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(response => !response.IsSuccessStatusCode),
                OnOpened = args =>
                {
                    _logger.LogError(
                        "BrokerAdeptClient circuit breaker opened (IR-001 outage): {Reason}",
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return default;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("BrokerAdeptClient circuit breaker closed: IR-001 recovered");
                    return default;
                }
            })
            .Build();
    }

    public async Task<AdeptNormalizationResult> NormalizeAsync(string rawPayloadRef, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async ct => await _httpClient.PostAsJsonAsync("normalize", new { rawPayloadRef }, ct),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new AdeptNormalizationResult(
                    false, null, null, null, null, null, null,
                    $"AdeptHttpError_{(int)response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<AdeptNormalizationResult>(cancellationToken);
            return result ?? new AdeptNormalizationResult(false, null, null, null, null, null, null, "AdeptEmptyResponse");
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("BrokerAdeptClient call skipped: circuit open (IR-001 outage)");
            return new AdeptNormalizationResult(false, null, null, null, null, null, null, "AdeptServiceUnavailable");
        }
    }
}

// DI registration (design.md Dependencies IR-001): typed HttpClient, base address supplied
// via configuration ("Adept:BaseUrl") once a real IR-001 endpoint exists to point at.
public static class BrokerAdeptClientServiceCollectionExtensions
{
    public static IServiceCollection AddBrokerAdeptClient(this IServiceCollection services)
    {
        services.AddHttpClient<IBrokerAdeptClient, BrokerAdeptClient>();
        return services;
    }
}
