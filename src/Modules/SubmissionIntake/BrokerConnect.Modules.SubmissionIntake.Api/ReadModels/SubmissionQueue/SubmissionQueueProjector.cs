using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionQueue;

// design.md Read Models: SubmissionQueue projector, Wolverine-subscriber pattern
// per Existing Patterns to Follow -- keyed by submissionId, which is the
// SubmissionNormalized event's own stream id, so no MultiStreamProjection is
// needed (mirrors NormalizeSubmissionViaAdeptHandler's pattern). Wolverine.Marten's
// IntegrateWithWolverine() forwards captured events to matching static Handle
// methods automatically (see Api.Host/Program.cs), so no explicit subscription
// wiring is needed here.
//
// Task 4.7.1 [FIX 4.7]: BrokerSubmissionReceived carries BrokerFirmId and the real
// ReceivedAt; SubmissionNormalized carries neither, so the row is upserted across
// both events -- BrokerSubmissionReceived creates it, SubmissionNormalized fills in
// the rest without clobbering the fields the first event set.
//
// SubmissionRoutingRejected retraction (delete-if-exists) lands in task 6.8; the
// duplicate-detection gap-fix (PotentialDuplicateSubmissionDetected /
// SubmissionSuperseded / SubmissionConfirmedDistinct flipping IsPossibleDuplicate/
// SuspectedOriginalSubmissionId) lands in task 5.5 as additional Handle overloads
// on this same class.
public sealed class SubmissionQueueProjector
{
    public static async Task Handle(
        BrokerSubmissionReceived @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        var queueItem = await session.LoadAsync<SubmissionQueue>(@event.SubmissionId, cancellationToken)
            ?? new SubmissionQueue { SubmissionId = @event.SubmissionId };

        queueItem.BrokerFirmId = @event.BrokerFirmId;
        queueItem.ReceivedAt = @event.ReceivedAt;

        session.Store(queueItem);
        await session.SaveChangesAsync(cancellationToken);
    }

    public static async Task Handle(
        SubmissionNormalized @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        var queueItem = await session.LoadAsync<SubmissionQueue>(@event.SubmissionId, cancellationToken)
            ?? new SubmissionQueue { SubmissionId = @event.SubmissionId };

        queueItem.ClassOfBusiness = @event.ClassOfBusiness;
        queueItem.Territory = @event.Territory;
        queueItem.NamedInsured = @event.NamedInsured;
        queueItem.LineSizeSought = @event.LineSizeSought;
        queueItem.Status = @event.NormalizationStatus;

        session.Store(queueItem);
        await session.SaveChangesAsync(cancellationToken);
    }

    // Task 5.5 gap-fix: flags the row for the newly-detected submission as a
    // possible duplicate (design.md Technical Decisions -- IsPossibleDuplicate/
    // SuspectedOriginalSubmissionId otherwise have no event source at all).
    public static async Task Handle(
        PotentialDuplicateSubmissionDetected @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        var queueItem = await session.LoadAsync<SubmissionQueue>(@event.SubmissionId, cancellationToken)
            ?? new SubmissionQueue { SubmissionId = @event.SubmissionId };

        queueItem.IsPossibleDuplicate = true;
        queueItem.SuspectedOriginalSubmissionId = @event.SuspectedOriginalSubmissionId;

        session.Store(queueItem);
        await session.SaveChangesAsync(cancellationToken);
    }

    // SubmissionSuperseded's doc comment: "confirms the new submission is a genuine
    // resubmission/update of the original" -- the original is now stale and should no
    // longer appear in the underwriter's active queue, so its row is removed
    // (delete-if-exists), mirroring the SubmissionRoutingRejected retraction pattern
    // documented in design.md's Edge Cases. Keyed by OriginalSubmissionId, the field
    // the event actually lands on (it's appended to the original's own stream).
    public static async Task Handle(
        SubmissionSuperseded @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        session.Delete<SubmissionQueue>(@event.OriginalSubmissionId);
        await session.SaveChangesAsync(cancellationToken);
    }

    // SubmissionConfirmedDistinct's doc comment: "both proceed independently" -- the
    // flagged submission's row stays in the active queue, but the possible-duplicate
    // flag is cleared since it's now confirmed to not be a duplicate.
    public static async Task Handle(
        SubmissionConfirmedDistinct @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        var queueItem = await session.LoadAsync<SubmissionQueue>(@event.SubmissionId, cancellationToken);
        if (queueItem is null)
        {
            return;
        }

        queueItem.IsPossibleDuplicate = false;
        queueItem.SuspectedOriginalSubmissionId = null;

        session.Store(queueItem);
        await session.SaveChangesAsync(cancellationToken);
    }

    // Task 6.8 -- design.md Edge Cases: "SubmissionRoutingRejected after normalization
    // already succeeded" -- RouteSubmissionOnReceipt and NormalizeSubmissionViaAdept run
    // in parallel off the same BrokerSubmissionReceived trigger (design's parallel-not-
    // sequential decision), so a SubmissionQueue row may already exist by the time the
    // (late) rejection lands. requirements.md's US-2 narrative is explicit: the
    // submission must "never appear in any underwriter's queue" once rejected, so the
    // row is retracted (delete-if-exists), mirroring SubmissionSuperseded's retraction
    // pattern above. Keyed by SubmissionId directly -- SubmissionRoutingRejected is
    // appended to the rejected submission's own stream (unlike SubmissionSuperseded's
    // OriginalSubmissionId indirection).
    public static async Task Handle(
        SubmissionRoutingRejected @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        session.Delete<SubmissionQueue>(@event.SubmissionId);
        await session.SaveChangesAsync(cancellationToken);
    }
}
