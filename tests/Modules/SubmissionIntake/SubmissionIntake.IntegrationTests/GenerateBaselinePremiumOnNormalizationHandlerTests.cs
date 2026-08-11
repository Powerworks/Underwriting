using BrokerConnect.Modules.SubmissionIntake.Api.Automations.GenerateBaselinePremiumOnNormalization;
using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using BrokerConnect.Modules.SubmissionIntake.Infrastructure;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace SubmissionIntake.IntegrationTests;

// 7.4 -- Layer 3 (Testcontainers-backed Postgres, real AggregateStreamAsync/FetchStreamAsync)
// per design.md Test Strategy Layer 3: IRatingEngineClient mocked (NSubstitute) at the
// boundary, the Marten interaction is what needs the real store, not the external HTTP
// call. Reuses the shared SubmissionIntakePostgresFixture/SubmissionIntakePostgresCollection
// (defined in NormalizeSubmissionViaAdeptHandlerTests.cs, not redefined) -- same fixture
// convention as 6.4/5.3/5.7.
//
// RED: GenerateBaselinePremiumOnNormalizationHandler does not exist yet (created in 7.5).
// This file is expected to fail to compile until then -- that is the correct RED state.
[Collection(SubmissionIntakePostgresCollection.Name)]
public class GenerateBaselinePremiumOnNormalizationHandlerTests(SubmissionIntakePostgresFixture fixture)
{
    private static BrokerSubmissionReceived ReceivedEvent(Guid submissionId) =>
        new(submissionId, "acme-brokers", "jane@acme.com", "raw-payload-ref-123", "api", DateTimeOffset.UtcNow);

    // classOfBusiness/territory/namedInsured suffixed with the submission's own id per the
    // 5.4.1 cross-test-class collision convention (this fixture's Postgres collection is
    // shared with DetectPotentialDuplicateOnNormalization's exact-match tests).
    private static SubmissionNormalized NormalizedEvent(Guid submissionId) => new(
        submissionId,
        $"Property-{submissionId:N}",
        $"Bermuda-{submissionId:N}",
        5_000_000m,
        "Standard terms",
        $"Acme Holdings LLC {submissionId:N}",
        DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
        "Normalized",
        DateTimeOffset.UtcNow);

    // (1) success -> BaselinePremiumGenerated appended
    [Fact]
    public async Task Successful_rating_engine_call_appends_BaselinePremiumGenerated()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var received = ReceivedEvent(submissionId);
        var normalized = NormalizedEvent(submissionId);
        session.Events.StartStream<Submission>(submissionId, received, normalized);
        await session.SaveChangesAsync();

        var client = Substitute.For<IRatingEngineClient>();
        client.GetBaselinePricingAsync(
                submissionId, normalized.ClassOfBusiness, normalized.Territory, normalized.LineSizeSought,
                Arg.Any<CancellationToken>())
            .Returns(new BaselinePricingResult(125_000m, new { windExposure = "High" }, "rating-model-v3"));

        await GenerateBaselinePremiumOnNormalizationHandler.Handle(
            normalized, session, client, NullLogger<GenerateBaselinePremiumOnNormalizationHandler>.Instance,
            CancellationToken.None);

        var submission = await session.Events.AggregateStreamAsync<Submission>(submissionId);
        submission.ShouldNotBeNull();
        submission.BaselinePremium.ShouldBe(125_000m);
        submission.ModelVersion.ShouldBe("rating-model-v3");
        submission.RiskFactorSummary.ShouldNotBeNullOrWhiteSpace();
    }

    // (2) BaselinePremium already set -> not re-priced (idempotency guard)
    [Fact]
    public async Task Already_priced_submission_is_not_re_priced()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var received = ReceivedEvent(submissionId);
        var normalized = NormalizedEvent(submissionId);
        session.Events.StartStream<Submission>(submissionId, received, normalized);
        await session.SaveChangesAsync();

        var client = Substitute.For<IRatingEngineClient>();
        client.GetBaselinePricingAsync(
                submissionId, normalized.ClassOfBusiness, normalized.Territory, normalized.LineSizeSought,
                Arg.Any<CancellationToken>())
            .Returns(new BaselinePricingResult(125_000m, new { windExposure = "High" }, "rating-model-v3"));

        // First delivery: prices as usual.
        await GenerateBaselinePremiumOnNormalizationHandler.Handle(
            normalized, session, client, NullLogger<GenerateBaselinePremiumOnNormalizationHandler>.Instance,
            CancellationToken.None);

        var events = await session.Events.FetchStreamAsync(submissionId);
        var countAfterFirstDelivery = events.Count;

        // Redelivery of the same trigger event (Architecture Constraints idempotency guard).
        await GenerateBaselinePremiumOnNormalizationHandler.Handle(
            normalized, session, client, NullLogger<GenerateBaselinePremiumOnNormalizationHandler>.Instance,
            CancellationToken.None);

        var eventsAfterRedelivery = await session.Events.FetchStreamAsync(submissionId);
        eventsAfterRedelivery.Count.ShouldBe(countAfterFirstDelivery);
        await client.Received(1).GetBaselinePricingAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }
}
