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

    // 8.4: resubmittedForNormalization guard (design.md Automations table: "guard:
    // resubmittedForNormalization == true"). Deliberately does NOT share the
    // BrokerSubmissionReceived overload's "NormalizationStatus already set" idempotency
    // guard above -- that check would permanently block re-normalization after any prior
    // SubmissionNormalizationFailed, defeating the entire purpose of a correction. This
    // overload's own idempotency concern (redelivery of the same SubmissionManuallyCorrected
    // event) has no aggregate-tracked state to guard against -- design.md's Submission
    // "Apply-computed state" table lists no field sourced from this event at all (8.1/8.2) --
    // so redelivery-safety here is an accepted gap, same shape as the IR-001/IR-005
    // "operational gap" documented in design.md's Error Handling table, not one this task
    // is scoped to close.
    public static async Task Handle(
        SubmissionManuallyCorrected @event,
        IDocumentSession session,
        IBrokerAdeptClient client,
        ILogger<NormalizeSubmissionViaAdeptHandler> logger,
        CancellationToken cancellationToken)
    {
        if (!@event.ResubmittedForNormalization)
        {
            logger.LogInformation(
                "Submission {SubmissionId} corrected without resubmission; skipping re-normalization",
                @event.SubmissionId);
            return;
        }

        var submission = await session.Events.AggregateStreamAsync<Submission>(
            @event.SubmissionId, token: cancellationToken);
        if (submission is null)
        {
            logger.LogWarning(
                "Submission {SubmissionId} not found for correction re-normalization", @event.SubmissionId);
            return;
        }

        var result = await client.NormalizeAsync(submission.RawPayloadRef, cancellationToken);

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
                "Submission {SubmissionId} re-normalized via ADEPT (IR-001) after correction", @event.SubmissionId);
        }
        else
        {
            session.Events.Append(@event.SubmissionId, new SubmissionNormalizationFailed(
                @event.SubmissionId,
                result.FailureReason ?? "AdeptUnknownFailure",
                submission.RawPayloadRef,
                DateTimeOffset.UtcNow));

            logger.LogWarning(
                "Submission {SubmissionId} re-normalization failed via ADEPT (IR-001) after correction: {FailureReason}",
                @event.SubmissionId, result.FailureReason);
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}
