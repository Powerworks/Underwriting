using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.BrokerAuthorizationExceptionLog;

// design.md Read Models: BrokerAuthorizationExceptionLog projector, Wolverine-subscriber
// pattern per Existing Patterns to Follow -- keyed by submissionId, which is the
// Submission stream's own id, so no MultiStreamProjection is needed (mirrors
// SubmissionQueueProjector's task-4.7 pattern). Wolverine.Marten's
// IntegrateWithWolverine() forwards captured events to matching static Handle methods
// automatically (see Api.Host/Program.cs), so no explicit subscription wiring is needed
// here.
//
// Task 6.6 field-tracing (per the 4.7.1/4.9 lesson): cross-checked every
// BrokerAuthorizationExceptionLog field (requirements.md Event Model Detail) against its
// real source event. submissionId, brokerFirmId, requestedCellId,
// requestedClassOfBusiness, rejectionReason, rejectedAt all map 1:1 onto
// SubmissionRoutingRejected -- unlike SubmissionQueue/SubmissionExceptionQueue, no other
// event needs to be subscribed here. reviewStatus has no source event at all (a genuine
// gap, not a missed field); it's defaulted to "Pending" on creation, same shape as
// SubmissionExceptionQueue.Status's "Failed" literal (task 4.9).
public sealed class BrokerAuthorizationExceptionLogProjector
{
    public static async Task Handle(
        SubmissionRoutingRejected @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        var logEntry = await session.LoadAsync<BrokerAuthorizationExceptionLog>(@event.SubmissionId, cancellationToken)
            ?? new BrokerAuthorizationExceptionLog { SubmissionId = @event.SubmissionId };

        logEntry.BrokerFirmId = @event.BrokerFirmId;
        logEntry.RequestedCellId = @event.RequestedCellId;
        logEntry.RequestedClassOfBusiness = @event.RequestedClassOfBusiness;
        logEntry.RejectionReason = @event.RejectionReason;
        logEntry.RejectedAt = @event.RejectedAt;
        logEntry.ReviewStatus = "Pending";

        session.Store(logEntry);
        await session.SaveChangesAsync(cancellationToken);
    }
}
