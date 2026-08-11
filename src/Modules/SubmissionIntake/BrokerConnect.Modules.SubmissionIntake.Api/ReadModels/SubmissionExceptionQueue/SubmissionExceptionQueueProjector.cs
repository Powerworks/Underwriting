using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionExceptionQueue;

// design.md Read Models: SubmissionExceptionQueue projector, Wolverine-subscriber
// pattern per Existing Patterns to Follow -- keyed by submissionId, which is the
// Submission stream's own id, so no MultiStreamProjection is needed (mirrors
// SubmissionQueueProjector's task-4.7 pattern). Wolverine.Marten's
// IntegrateWithWolverine() forwards captured events to matching static Handle
// methods automatically (see Api.Host/Program.cs), so no explicit subscription
// wiring is needed here.
//
// Task 4.9: cross-checked every SubmissionExceptionQueue field (requirements.md
// Event Model Detail) against its real source event before wiring only
// SubmissionNormalizationFailed (per the 4.7.1 lesson). failureReason/attemptedAt
// come from SubmissionNormalizationFailed, but brokerFirmId does not -- that event
// carries no such field. BrokerSubmissionReceived is the only event with
// brokerFirmId, so this projector upserts across both events, same shape as
// SubmissionQueueProjector's 4.7.1 fix: BrokerSubmissionReceived creates the row
// with BrokerFirmId, SubmissionNormalizationFailed fills in the rest without
// clobbering it.
//
public sealed class SubmissionExceptionQueueProjector
{
    public static async Task Handle(
        BrokerSubmissionReceived @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        var queueItem = await session.LoadAsync<SubmissionExceptionQueue>(@event.SubmissionId, cancellationToken)
            ?? new SubmissionExceptionQueue { SubmissionId = @event.SubmissionId };

        queueItem.BrokerFirmId = @event.BrokerFirmId;

        session.Store(queueItem);
        await session.SaveChangesAsync(cancellationToken);
    }

    public static async Task Handle(
        SubmissionNormalizationFailed @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        var queueItem = await session.LoadAsync<SubmissionExceptionQueue>(@event.SubmissionId, cancellationToken)
            ?? new SubmissionExceptionQueue { SubmissionId = @event.SubmissionId };

        queueItem.FailureReason = @event.FailureReason;
        queueItem.AttemptedAt = @event.AttemptedAt;
        queueItem.Status = "Failed";

        session.Store(queueItem);
        await session.SaveChangesAsync(cancellationToken);
    }

    // Task 8.5 gap-fix (design.md Technical Decisions): "status field otherwise has no
    // event that can ever move it off its initial value" -- DEC-008 (resolved):
    // entries persist indefinitely until SubmissionManuallyCorrected. Status-only
    // update; if no row exists yet (correction landed before any normalization
    // failure was ever recorded here), no-op rather than creating a row with no
    // failureReason/attemptedAt of its own.
    public static async Task Handle(
        SubmissionManuallyCorrected @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        var queueItem = await session.LoadAsync<SubmissionExceptionQueue>(@event.SubmissionId, cancellationToken);
        if (queueItem is null)
        {
            return;
        }

        queueItem.Status = "Corrected";

        session.Store(queueItem);
        await session.SaveChangesAsync(cancellationToken);
    }
}
