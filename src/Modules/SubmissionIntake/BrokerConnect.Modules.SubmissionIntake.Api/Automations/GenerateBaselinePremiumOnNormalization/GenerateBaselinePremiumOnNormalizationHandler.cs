using System.Text.Json;
using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using BrokerConnect.Modules.SubmissionIntake.Infrastructure;
using Marten;
using Microsoft.Extensions.Logging;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Automations.GenerateBaselinePremiumOnNormalization;

// design.md Automations: triggered by SubmissionNormalized (success only, S1a.5 -- this
// event type only ever fires on a successful normalization, see SubmissionNormalized's
// own doc comment) -- calls IRatingEngineClient (IR-005) with the normalized fields,
// appending BaselinePremiumGenerated. Wolverine.Marten's IntegrateWithWolverine()
// forwards captured events to matching local Handle methods automatically (see
// Program.cs), so no explicit subscription wiring is needed here.
public sealed class GenerateBaselinePremiumOnNormalizationHandler
{
    public static async Task Handle(
        SubmissionNormalized @event,
        IDocumentSession session,
        IRatingEngineClient client,
        ILogger<GenerateBaselinePremiumOnNormalizationHandler> logger,
        CancellationToken cancellationToken)
    {
        // Architecture Constraints idempotency: BaselinePremium already set means this
        // trigger event was redelivered (or pricing already ran) -- no-op.
        var submission = await session.Events.AggregateStreamAsync<Submission>(
            @event.SubmissionId, token: cancellationToken);
        if (submission?.BaselinePremium is not null)
        {
            logger.LogInformation(
                "Submission {SubmissionId} already priced (baseline premium {BaselinePremium}); skipping redelivered {EventName}",
                @event.SubmissionId, submission.BaselinePremium, nameof(SubmissionNormalized));
            return;
        }

        var result = await client.GetBaselinePricingAsync(
            @event.SubmissionId, @event.ClassOfBusiness, @event.Territory, @event.LineSizeSought, cancellationToken);

        // BaselinePricingResult.RiskFactorSummary is design.md's "Custom" object type
        // (Interfaces section); the domain event's field is a string (SubmissionIntakeEvents.cs,
        // set since 2.1) -- serialized to JSON here at the automation boundary, same
        // "specialist capability requested and displayed, never computed" discipline
        // (requirements.md AC-9.1) applied to how it's stored, not just how it's derived.
        session.Events.Append(@event.SubmissionId, new BaselinePremiumGenerated(
            @event.SubmissionId,
            result.BaselinePremium,
            JsonSerializer.Serialize(result.RiskFactorSummary),
            result.ModelVersion,
            DateTimeOffset.UtcNow));

        await session.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Submission {SubmissionId} priced via rating engine (IR-005), baseline premium {BaselinePremium}",
            @event.SubmissionId, result.BaselinePremium);
    }
}
