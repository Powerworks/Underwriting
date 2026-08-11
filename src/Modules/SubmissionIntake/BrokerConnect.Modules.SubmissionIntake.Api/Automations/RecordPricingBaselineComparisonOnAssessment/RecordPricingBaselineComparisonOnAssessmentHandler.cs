using BrokerConnect.Modules.SubmissionIntake.Api.IntegrationEvents.Consumers;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;
using Microsoft.Extensions.Logging;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Automations.RecordPricingBaselineComparisonOnAssessment;

// design.md Automations table: consumes the assumed SubmissionAssessedV1 cross-module
// integration event (ADR-004: durable per-module RabbitMQ queue, never a direct
// cross-module call -- see Module.cs's IntegrationEventQueueName override and
// Program.cs's generic ListenToRabbitQueue wiring). Compares proposedPremium to
// BaselinePremiumGenerated.baselinePremium, computed live via
// AggregateStreamAsync<SubmissionPricingState> (never persisted, per Architecture
// Constraints) -- appends PricingBaselineAccepted (exact match) or
// PricingBaselineOverridden (variance computed) onto the Submission stream itself
// (design.md Technical Decisions: both events are aggregate: Submission even though
// AssessSubmission, the triggering command, belongs to Underwriting Decisioning).
public sealed class RecordPricingBaselineComparisonOnAssessmentHandler
{
    public static async Task Handle(
        SubmissionAssessedV1 @event,
        IDocumentSession session,
        ILogger<RecordPricingBaselineComparisonOnAssessmentHandler> logger,
        CancellationToken cancellationToken)
    {
        var pricingState = await session.Events.AggregateStreamAsync<SubmissionPricingState>(
            @event.SubmissionId, token: cancellationToken);

        // design.md Error Handling: "RecordPricingBaselineComparisonOnAssessment fires
        // before BaselinePremiumGenerated exists ... Guard: no-op / defer (log and
        // skip) rather than fail -- should not happen in practice ... but not provably
        // ordered from this module's view." Also covers a submission that doesn't
        // exist at all (pricingState is null when the stream has zero events).
        if (pricingState?.BaselinePremium is not { } baselinePremium)
        {
            logger.LogInformation(
                "Submission {SubmissionId} has no BaselinePremiumGenerated yet; deferring pricing-baseline comparison",
                @event.SubmissionId);
            return;
        }

        // Idempotency (Architecture Constraints): redelivered SubmissionAssessedV1 when
        // a comparison already exists for this submission -- no-op.
        if (pricingState.HasComparisonRecorded)
        {
            logger.LogInformation(
                "Submission {SubmissionId} already has a pricing-baseline comparison recorded; skipping redelivered {EventName}",
                @event.SubmissionId, nameof(SubmissionAssessedV1));
            return;
        }

        if (@event.ProposedPremium == baselinePremium)
        {
            session.Events.Append(@event.SubmissionId, new PricingBaselineAccepted(
                @event.SubmissionId, baselinePremium, @event.UnderwriterId, AcceptedAt: DateTimeOffset.UtcNow));

            logger.LogInformation(
                "Submission {SubmissionId} assessment matches baseline premium exactly", @event.SubmissionId);
        }
        else
        {
            // No board-specified sign convention for variance -- signed (proposed minus
            // baseline) rather than absolute value, so actuarial can see over/under
            // direction, matching US-10/11's "measure how often/how much underwriters
            // deviate from the model" narrative.
            var variance = @event.ProposedPremium - baselinePremium;

            session.Events.Append(@event.SubmissionId, new PricingBaselineOverridden(
                @event.SubmissionId, baselinePremium, @event.ProposedPremium, variance,
                @event.UnderwriterId, OverriddenAt: DateTimeOffset.UtcNow));

            logger.LogInformation(
                "Submission {SubmissionId} assessment diverges from baseline premium by {Variance}",
                @event.SubmissionId, variance);
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}
