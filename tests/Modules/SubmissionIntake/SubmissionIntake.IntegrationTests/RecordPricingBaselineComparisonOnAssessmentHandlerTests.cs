using BrokerConnect.Modules.SubmissionIntake.Api.Automations.RecordPricingBaselineComparisonOnAssessment;
using BrokerConnect.Modules.SubmissionIntake.Api.IntegrationEvents.Consumers;
using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace SubmissionIntake.IntegrationTests;

// 10.4 -- Layer 3 (Testcontainers-backed Postgres, real AggregateStreamAsync into
// SubmissionPricingState), same shared fixture as 4.4/5.7/8.3/9.3
// (SubmissionIntakePostgresFixture). Per design.md Automations table:
// RecordPricingBaselineComparisonOnAssessment consumes the assumed SubmissionAssessedV1
// cross-module integration event, comparing proposedPremium to
// BaselinePremiumGenerated.baselinePremium (computed live via SubmissionPricingState,
// never persisted). This project's Layer 3 convention calls handler methods directly
// rather than through a running Wolverine/RabbitMQ host (matching every other
// automation test in this suite).
//
// RED: RecordPricingBaselineComparisonOnAssessmentHandler does not exist yet (created
// in 10.5). This file is expected to fail to compile until then -- that is the correct
// RED state.
[Collection(SubmissionIntakePostgresCollection.Name)]
public class RecordPricingBaselineComparisonOnAssessmentHandlerTests(SubmissionIntakePostgresFixture fixture)
{
    private static BrokerSubmissionReceived ReceivedEvent(Guid submissionId) =>
        new(submissionId, "acme-brokers", "jane@acme.com", "raw-payload-ref-123", "api", DateTimeOffset.UtcNow);

    private static BaselinePremiumGenerated GeneratedEvent(Guid submissionId, decimal baselinePremium) => new(
        submissionId, baselinePremium, "{\"windExposure\":\"High\"}", "rating-model-v3", DateTimeOffset.UtcNow);

    // (1) proposedPremium == baselinePremium exactly -> PricingBaselineAccepted
    [Fact]
    public async Task Exact_match_appends_PricingBaselineAccepted()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        session.Events.StartStream<Submission>(submissionId, ReceivedEvent(submissionId));
        session.Events.Append(submissionId, GeneratedEvent(submissionId, 125_000m));
        await session.SaveChangesAsync();

        var assessed = new SubmissionAssessedV1(submissionId, 125_000m, "underwriter-1", DateTimeOffset.UtcNow);

        await RecordPricingBaselineComparisonOnAssessmentHandler.Handle(
            assessed, session, NullLogger<RecordPricingBaselineComparisonOnAssessmentHandler>.Instance, CancellationToken.None);

        var events = await session.Events.FetchStreamAsync(submissionId);
        var accepted = events.Select(e => e.Data).OfType<PricingBaselineAccepted>().Single();
        accepted.BaselinePremium.ShouldBe(125_000m);
        accepted.UnderwriterId.ShouldBe("underwriter-1");
        events.Select(e => e.Data).OfType<PricingBaselineOverridden>().ShouldBeEmpty();
    }

    // (2) proposedPremium diverges -> PricingBaselineOverridden with computed variance
    [Fact]
    public async Task Divergence_appends_PricingBaselineOverridden_with_computed_variance()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        session.Events.StartStream<Submission>(submissionId, ReceivedEvent(submissionId));
        session.Events.Append(submissionId, GeneratedEvent(submissionId, 125_000m));
        await session.SaveChangesAsync();

        var assessed = new SubmissionAssessedV1(submissionId, 140_000m, "underwriter-1", DateTimeOffset.UtcNow);

        await RecordPricingBaselineComparisonOnAssessmentHandler.Handle(
            assessed, session, NullLogger<RecordPricingBaselineComparisonOnAssessmentHandler>.Instance, CancellationToken.None);

        var events = await session.Events.FetchStreamAsync(submissionId);
        var overridden = events.Select(e => e.Data).OfType<PricingBaselineOverridden>().Single();
        overridden.BaselinePremium.ShouldBe(125_000m);
        overridden.ProposedPremium.ShouldBe(140_000m);
        overridden.Variance.ShouldBe(15_000m);
        overridden.UnderwriterId.ShouldBe("underwriter-1");
        events.Select(e => e.Data).OfType<PricingBaselineAccepted>().ShouldBeEmpty();
    }

    // (3) redelivered SubmissionAssessedV1 when a comparison already exists -> no duplicate append
    [Fact]
    public async Task Redelivered_event_after_comparison_already_recorded_does_not_duplicate_append()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        session.Events.StartStream<Submission>(submissionId, ReceivedEvent(submissionId));
        session.Events.Append(submissionId, GeneratedEvent(submissionId, 125_000m));
        await session.SaveChangesAsync();

        var assessed = new SubmissionAssessedV1(submissionId, 125_000m, "underwriter-1", DateTimeOffset.UtcNow);

        await RecordPricingBaselineComparisonOnAssessmentHandler.Handle(
            assessed, session, NullLogger<RecordPricingBaselineComparisonOnAssessmentHandler>.Instance, CancellationToken.None);

        var countAfterFirstDelivery = (await session.Events.FetchStreamAsync(submissionId)).Count;

        await RecordPricingBaselineComparisonOnAssessmentHandler.Handle(
            assessed, session, NullLogger<RecordPricingBaselineComparisonOnAssessmentHandler>.Instance, CancellationToken.None);

        var eventsAfterRedelivery = await session.Events.FetchStreamAsync(submissionId);
        eventsAfterRedelivery.Count.ShouldBe(countAfterFirstDelivery);
    }

    // (4) BaselinePremiumGenerated doesn't exist yet -> no-op/defer, not a failure
    // (design.md Error Handling: "Guard: no-op / defer (log and skip) rather than fail").
    [Fact]
    public async Task Missing_baseline_premium_is_a_no_op_not_a_failure()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        session.Events.StartStream<Submission>(submissionId, ReceivedEvent(submissionId));
        await session.SaveChangesAsync();

        var assessed = new SubmissionAssessedV1(submissionId, 125_000m, "underwriter-1", DateTimeOffset.UtcNow);

        await Should.NotThrowAsync(() => RecordPricingBaselineComparisonOnAssessmentHandler.Handle(
            assessed, session, NullLogger<RecordPricingBaselineComparisonOnAssessmentHandler>.Instance, CancellationToken.None));

        var events = await session.Events.FetchStreamAsync(submissionId);
        events.Select(e => e.Data).OfType<PricingBaselineAccepted>().ShouldBeEmpty();
        events.Select(e => e.Data).OfType<PricingBaselineOverridden>().ShouldBeEmpty();
    }
}
