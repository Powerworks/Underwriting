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
}
