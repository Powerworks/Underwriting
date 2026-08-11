using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;
using Microsoft.Extensions.Logging;
using SubmissionQueueDoc = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionQueue.SubmissionQueue;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Automations.DetectPotentialDuplicateOnNormalization;

// design.md Automations: triggered by SubmissionNormalized (success only) -- exact-field
// match (classOfBusiness + territory + namedInsured, DEC-010) against other open
// (non-superseded, non-confirmed-distinct) submissions. Appends
// PotentialDuplicateSubmissionDetected on the NEW submission's own stream if found.
// Per design.md Non-Functional/Performance notes: query SubmissionQueue with an
// indexed Marten filter on classOfBusiness/territory, then match namedInsured
// in-memory. Wolverine.Marten's IntegrateWithWolverine() forwards captured events
// to matching local Handle methods automatically (see Program.cs), so no explicit
// subscription wiring is needed here.
public sealed class DetectPotentialDuplicateOnNormalizationHandler
{
    public static async Task Handle(
        SubmissionNormalized @event,
        IDocumentSession session,
        ILogger<DetectPotentialDuplicateOnNormalizationHandler> logger,
        CancellationToken cancellationToken)
    {
        // Architecture Constraints idempotency: IsPossibleDuplicate already true means
        // this trigger event was redelivered (or detection already ran) -- no-op.
        var submission = await session.Events.AggregateStreamAsync<Submission>(
            @event.SubmissionId, token: cancellationToken);
        if (submission?.IsPossibleDuplicate == true)
        {
            logger.LogInformation(
                "Submission {SubmissionId} already flagged as possible duplicate; skipping redelivered {EventName}",
                @event.SubmissionId, nameof(SubmissionNormalized));
            return;
        }

        var candidates = await session.Query<SubmissionQueueDoc>()
            .Where(q => q.ClassOfBusiness == @event.ClassOfBusiness && q.Territory == @event.Territory)
            .ToListAsync(cancellationToken);

        // Most-recently-received candidate wins when more than one open submission
        // exact-matches -- the freshest other submission is the most plausible
        // suspected original.
        var match = candidates
            .Where(q => q.SubmissionId != @event.SubmissionId && q.NamedInsured == @event.NamedInsured)
            .OrderByDescending(q => q.ReceivedAt)
            .FirstOrDefault();

        if (match is null)
        {
            return;
        }

        session.Events.Append(@event.SubmissionId, new PotentialDuplicateSubmissionDetected(
            @event.SubmissionId,
            match.SubmissionId,
            "classOfBusiness+territory+namedInsured",
            null,
            DateTimeOffset.UtcNow));

        logger.LogInformation(
            "Submission {SubmissionId} flagged as possible duplicate of {SuspectedOriginalSubmissionId}",
            @event.SubmissionId, match.SubmissionId);

        await session.SaveChangesAsync(cancellationToken);
    }
}
