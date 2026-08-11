using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Shouldly;
using Xunit;
using PricedSubmissionViewDoc = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.PricedSubmissionView.PricedSubmissionView;
using PricedSubmissionViewProjector = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.PricedSubmissionView.PricedSubmissionViewProjector;

namespace SubmissionIntake.IntegrationTests;

// 7.7 -- Layer 3 (Testcontainers-backed Postgres, real IDocumentSession.Store/Load) per
// design.md Test Strategy Layer 3, same fixture as 4.4's NormalizeSubmissionViaAdeptHandlerTests
// (shared collection). design.md's Read Models table lists 3 source events for this
// projector: BaselinePremiumGenerated, SubmissionConfirmedDistinct (board-wired), and
// the gap-fixed SubmissionNormalized (for brokerRequestedTerms).
[Collection(SubmissionIntakePostgresCollection.Name)]
public class PricedSubmissionViewProjectorTests(SubmissionIntakePostgresFixture fixture)
{
    // Suffixed with the submission's own id so real rows this test persists can never
    // collide with other tests sharing this fixture's Postgres collection (5.4.1
    // convention).
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

    [Fact]
    public async Task BaselinePremiumGenerated_populates_baseline_pricing_fields()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var generated = new BaselinePremiumGenerated(
            submissionId, 125_000m, "{\"windExposure\":\"High\"}", "rating-model-v3", DateTimeOffset.UtcNow);

        await PricedSubmissionViewProjector.Handle(generated, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var view = await querySession.LoadAsync<PricedSubmissionViewDoc>(submissionId);

        view.ShouldNotBeNull();
        view.SubmissionId.ShouldBe(submissionId);
        view.BaselinePremium.ShouldBe(125_000m);
        view.RiskFactorSummary.ShouldBe("{\"windExposure\":\"High\"}");
        view.ModelVersion.ShouldBe("rating-model-v3");
    }

    [Fact]
    public async Task SubmissionNormalized_populates_brokerRequestedTerms_gap_fix()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var normalized = NormalizedEvent(submissionId);

        await PricedSubmissionViewProjector.Handle(normalized, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var view = await querySession.LoadAsync<PricedSubmissionViewDoc>(submissionId);

        view.ShouldNotBeNull();
        view.BrokerRequestedTerms.ShouldNotBeNullOrWhiteSpace();
        view.BrokerRequestedTerms.ShouldContain($"Property-{submissionId:N}");
        view.BrokerRequestedTerms.ShouldContain($"Acme Holdings LLC {submissionId:N}");
    }

    [Fact]
    public async Task SubmissionConfirmedDistinct_ensures_row_exists()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var suspectedOriginalId = Guid.NewGuid();
        var confirmedDistinct = new SubmissionConfirmedDistinct(
            submissionId, suspectedOriginalId, "jane.underwriter", DateTimeOffset.UtcNow);

        await PricedSubmissionViewProjector.Handle(confirmedDistinct, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var view = await querySession.LoadAsync<PricedSubmissionViewDoc>(submissionId);

        view.ShouldNotBeNull();
        view.SubmissionId.ShouldBe(submissionId);
    }
}
