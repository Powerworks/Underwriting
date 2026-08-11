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
// Task 4.7 only wires SubmissionNormalized. SubmissionRoutingRejected retraction
// (delete-if-exists) lands in task 6.8; the duplicate-detection gap-fix
// (PotentialDuplicateSubmissionDetected / SubmissionSuperseded /
// SubmissionConfirmedDistinct flipping IsPossibleDuplicate/
// SuspectedOriginalSubmissionId) lands in task 5.5 as additional Handle overloads
// on this same class.
public sealed class SubmissionQueueProjector
{
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

        // SubmissionNormalized carries neither BrokerFirmId nor a receipt timestamp
        // (both live only on BrokerSubmissionReceived, which design.md's board-sourced
        // dependency edges for SubmissionQueue never wire this projector to). NormalizedAt
        // is the closest available timestamp; BrokerFirmId is left at its default until a
        // later task (if any) wires BrokerSubmissionReceived in too. Flagged, not silently
        // invented -- same convention as 001's CellAuthorityRegister limitation note.
        queueItem.ReceivedAt = @event.NormalizedAt;

        session.Store(queueItem);
        await session.SaveChangesAsync(cancellationToken);
    }
}
