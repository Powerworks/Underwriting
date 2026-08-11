using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace BrokerConnect.Modules.SubmissionIntake.Infrastructure;

// IR-005: AI baseline pricing engine, owned here as a dedicated anti-corruption-layer
// HTTP client (Solution Arch §4.3, ADR-008), same shape as IBrokerAdeptClient (IR-001).
// design.md Interfaces. "Rating computation ... is a specialist capability this system
// requests and displays, never computes" (requirements.md AC-9.1) -- this client is the
// boundary that enforces that discipline.
public interface IRatingEngineClient
{
    Task<BaselinePricingResult> GetBaselinePricingAsync(
        Guid submissionId, string classOfBusiness, string territory, decimal lineSizeSought,
        CancellationToken cancellationToken);
}

public sealed record BaselinePricingResult(decimal BaselinePremium, object RiskFactorSummary, string ModelVersion);

// Thrown when IR-005 is unavailable (Polly retries exhausted / circuit open) or
// responds with a non-success status. Unlike IR-001 (which has an explicit
// SubmissionNormalizationFailed domain event to model a definite failure),
// BaselinePremiumGenerated has no failure counterpart in the event model
// (requirements.md Event Model Detail) -- design.md's Error Handling table covers this
// exact case for both clients together: "BrokerAdeptClient/RatingEngineClient
// unavailable (IR-001/IR-005 outage) ... the submission simply has no [event] yet --
// a stuck-pending state ... flagged as an operational gap". Throwing (rather than
// returning a Succeeded=false sentinel, which the event model has no outlet for) lets
// the failure propagate to Wolverine's own message-level retry/redelivery, matching
// that stuck-pending characterization instead of inventing a domain event this spec
// doesn't define.
public sealed class RatingEngineUnavailableException(string message) : Exception(message);

// ADR-008: retry with jittered backoff + circuit breaker; breaker-open state is logged/
// alertable, never silently retried forever -- same policy shape as BrokerAdeptClient
// (4.1).
public sealed class RatingEngineClient : IRatingEngineClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RatingEngineClient> _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;

    public RatingEngineClient(HttpClient httpClient, ILogger<RatingEngineClient> logger)
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
                        "RatingEngineClient circuit breaker opened (IR-005 outage): {Reason}",
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return default;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("RatingEngineClient circuit breaker closed: IR-005 recovered");
                    return default;
                }
            })
            .Build();
    }

    public async Task<BaselinePricingResult> GetBaselinePricingAsync(
        Guid submissionId, string classOfBusiness, string territory, decimal lineSizeSought,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async ct => await _httpClient.PostAsJsonAsync(
                    "baseline-pricing", new { submissionId, classOfBusiness, territory, lineSizeSought }, ct),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new RatingEngineUnavailableException($"RatingEngineHttpError_{(int)response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<BaselinePricingResult>(cancellationToken);
            return result ?? throw new RatingEngineUnavailableException("RatingEngineEmptyResponse");
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("RatingEngineClient call skipped: circuit open (IR-005 outage)");
            throw new RatingEngineUnavailableException("RatingEngineServiceUnavailable");
        }
    }
}

// DI registration (design.md Dependencies IR-005): typed HttpClient, base address
// supplied via configuration ("RatingEngine:BaseUrl") once a real IR-005 endpoint
// exists to point at -- mirrors AddBrokerAdeptClient (4.1).
public static class RatingEngineClientServiceCollectionExtensions
{
    public static IServiceCollection AddRatingEngineClient(this IServiceCollection services)
    {
        services.AddHttpClient<IRatingEngineClient, RatingEngineClient>();
        return services;
    }
}
