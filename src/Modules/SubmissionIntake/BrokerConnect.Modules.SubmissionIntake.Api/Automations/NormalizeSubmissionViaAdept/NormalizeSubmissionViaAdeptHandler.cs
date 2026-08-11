using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using BrokerConnect.Modules.SubmissionIntake.Infrastructure;
using Marten;
using Microsoft.Extensions.Logging;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Automations.NormalizeSubmissionViaAdept;

// design.md Automations: triggered by BrokerSubmissionReceived (and, once Phase 8 wires
// the resubmittedForNormalization guard, SubmissionManuallyCorrected) -- calls
// IBrokerAdeptClient (IR-001) to extract structured fields from rawPayloadRef, appending
// SubmissionNormalized or SubmissionNormalizationFailed. Wolverine.Marten's
// IntegrateWithWolverine() forwards captured events to matching local Handle methods
// automatically (see Program.cs), so no explicit subscription wiring is needed here.
public sealed class NormalizeSubmissionViaAdeptHandler
{
    public static async Task Handle(
        BrokerSubmissionReceived @event,
        IDocumentSession session,
        IBrokerAdeptClient client,
        ILogger<NormalizeSubmissionViaAdeptHandler> logger,
        CancellationToken cancellationToken)
    {
        // Architecture Constraints idempotency: NormalizationStatus already set means
        // this trigger event was redelivered (or normalization already ran) -- no-op.
        var submission = await session.Events.AggregateStreamAsync<Submission>(
            @event.SubmissionId, token: cancellationToken);
        if (submission?.NormalizationStatus is not null)
        {
            logger.LogInformation(
                "Submission {SubmissionId} already normalized (status {NormalizationStatus}); skipping redelivered {EventName}",
                @event.SubmissionId, submission.NormalizationStatus, nameof(BrokerSubmissionReceived));
            return;
        }

        var result = await client.NormalizeAsync(@event.RawPayloadRef, cancellationToken);

        if (result.Succeeded)
        {
            session.Events.Append(@event.SubmissionId, new SubmissionNormalized(
                @event.SubmissionId,
                result.ClassOfBusiness!,
                result.Territory!,
                result.LineSizeSought!.Value,
                result.KeyTerms,
                result.NamedInsured!,
                result.EffectiveDateRequested!.Value,
                "Normalized",
                DateTimeOffset.UtcNow));

            logger.LogInformation(
                "Submission {SubmissionId} normalized via ADEPT (IR-001)", @event.SubmissionId);
        }
        else
        {
            session.Events.Append(@event.SubmissionId, new SubmissionNormalizationFailed(
                @event.SubmissionId,
                result.FailureReason ?? "AdeptUnknownFailure",
                @event.RawPayloadRef,
                DateTimeOffset.UtcNow));

            logger.LogWarning(
                "Submission {SubmissionId} normalization failed via ADEPT (IR-001): {FailureReason}",
                @event.SubmissionId, result.FailureReason);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    // Trigger wiring per design.md Automations table -- subscribed now so Wolverine's
    // event-forwarding picks it up, but the resubmittedForNormalization guard (and the
    // actual re-normalization behavior it gates) isn't wired until Phase 8, when
    // Submission.Apply(SubmissionManuallyCorrected) also lands. Deliberately inert until
    // then.
    public static Task Handle(SubmissionManuallyCorrected @event, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
