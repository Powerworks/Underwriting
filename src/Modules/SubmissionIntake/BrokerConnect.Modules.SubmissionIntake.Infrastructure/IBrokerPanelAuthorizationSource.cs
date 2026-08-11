using Microsoft.Extensions.DependencyInjection;

namespace BrokerConnect.Modules.SubmissionIntake.Infrastructure;

// design.md Unresolved Questions: RouteSubmissionOnReceipt's broker-panel-authorization
// data source is not confirmed by any board dependency edge (likely Authority
// Administration, context 0, but unverified). Stubbed behind this interface so
// RouteSubmissionOnReceipt (Implementation Step 8) is fully testable now and swappable
// for a real data source later without touching the handler.
public interface IBrokerPanelAuthorizationSource
{
    Task<bool> IsAuthorizedAsync(
        string brokerFirmId, string? cellIdHint, string? classOfBusinessHint, CancellationToken cancellationToken);
}

// STAND-IN pending Unresolved Questions resolution: always authorizes (never blocks).
// DEC-009 is a hard block on real unauthorized data, but until the actual data source is
// confirmed, blocking here would incorrectly reject every submission. Replace with a real
// implementation once the data source (likely Authority Administration) is confirmed.
public sealed class AlwaysAuthorizedBrokerPanelAuthorizationSource : IBrokerPanelAuthorizationSource
{
    public Task<bool> IsAuthorizedAsync(
        string brokerFirmId, string? cellIdHint, string? classOfBusinessHint, CancellationToken cancellationToken)
        => Task.FromResult(true);
}

// DI registration (design.md Unresolved Questions / Implementation Step 8).
public static class BrokerPanelAuthorizationSourceServiceCollectionExtensions
{
    public static IServiceCollection AddBrokerPanelAuthorizationSource(this IServiceCollection services)
    {
        services.AddSingleton<IBrokerPanelAuthorizationSource, AlwaysAuthorizedBrokerPanelAuthorizationSource>();
        return services;
    }
}
