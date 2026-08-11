using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using BrokerConnect.Modules.SubmissionIntake.Infrastructure;
using Marten;
using Microsoft.Extensions.Logging;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Automations.RouteSubmissionOnReceipt;

// design.md Automations: triggered by BrokerSubmissionReceived -- checks broker panel
// authorization for the requested cellIdHint/classOfBusinessHint (DEC-009: hard block, no
// broker feedback either way). Appends SubmissionRoutingRejected on the same stream if
// unauthorized; no-op if authorized. Wolverine.Marten's IntegrateWithWolverine() forwards
// captured events to matching local Handle methods automatically (see Program.cs), so no
// explicit subscription wiring is needed here.
public sealed class RouteSubmissionOnReceiptHandler
{
    public static async Task Handle(
        BrokerSubmissionReceived @event,
        IDocumentSession session,
        IBrokerPanelAuthorizationSource authorizationSource,
        ILogger<RouteSubmissionOnReceiptHandler> logger,
        CancellationToken cancellationToken)
    {
        // Architecture Constraints idempotency: IsRoutingRejected already true means this
        // trigger event was redelivered (or routing already ran) -- no-op.
        var submission = await session.Events.AggregateStreamAsync<Submission>(
            @event.SubmissionId, token: cancellationToken);
        if (submission?.IsRoutingRejected == true)
        {
            logger.LogInformation(
                "Submission {SubmissionId} routing already rejected; skipping redelivered {EventName}",
                @event.SubmissionId, nameof(BrokerSubmissionReceived));
            return;
        }

        var isAuthorized = await authorizationSource.IsAuthorizedAsync(
            @event.BrokerFirmId, @event.CellIdHint, @event.ClassOfBusinessHint, cancellationToken);

        if (isAuthorized)
        {
            return;
        }

        session.Events.Append(@event.SubmissionId, new SubmissionRoutingRejected(
            @event.SubmissionId,
            @event.BrokerFirmId,
            @event.CellIdHint ?? string.Empty,
            @event.ClassOfBusinessHint,
            "Broker panel is not authorized for the requested cell/class of business",
            DateTimeOffset.UtcNow));

        logger.LogInformation(
            "Submission {SubmissionId} routing rejected -- broker {BrokerFirmId} not authorized",
            @event.SubmissionId, @event.BrokerFirmId);

        await session.SaveChangesAsync(cancellationToken);
    }
}
