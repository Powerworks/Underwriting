using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Shouldly;
using Xunit;
using BrokerAuthorizationExceptionLogDoc = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.BrokerAuthorizationExceptionLog.BrokerAuthorizationExceptionLog;
using BrokerAuthorizationExceptionLogProjector = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.BrokerAuthorizationExceptionLog.BrokerAuthorizationExceptionLogProjector;

namespace SubmissionIntake.IntegrationTests;

// 6.6 -- Layer 3 (Testcontainers-backed Postgres, real IDocumentSession.Store/Load) per
// design.md Test Strategy Layer 3, same fixture as 4.7/4.9's projector tests (shared
// collection).
[Collection(SubmissionIntakePostgresCollection.Name)]
public class BrokerAuthorizationExceptionLogProjectorTests(SubmissionIntakePostgresFixture fixture)
{
    // 5.4.1 convention: GUID-suffixed seed data so no two tests across the whole
    // IntegrationTests project can collide on shared-fixture state.
    private static SubmissionRoutingRejected RejectedEvent(Guid submissionId, string suffix, DateTimeOffset rejectedAt) => new(
        submissionId,
        $"Acme Brokerage LLC {suffix}",
        $"cell-outside-panel-{suffix}",
        $"Class-{suffix}",
        $"Panel authorization does not cover requested cell {suffix}",
        rejectedAt);

    // Submitting SubmissionRoutingRejected alone produces a full
    // BrokerAuthorizationExceptionLog row -- every field except reviewStatus maps 1:1
    // onto this event (task 6.6 field-tracing); reviewStatus defaults to "Pending"
    // since no event carries it.
    [Fact]
    public async Task SubmissionRoutingRejected_produces_BrokerAuthorizationExceptionLog_row()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var rejectedAt = DateTimeOffset.UtcNow;
        var rejected = RejectedEvent(submissionId, suffix, rejectedAt);

        await BrokerAuthorizationExceptionLogProjector.Handle(rejected, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var logEntry = await querySession.LoadAsync<BrokerAuthorizationExceptionLogDoc>(submissionId);

        logEntry.ShouldNotBeNull();
        logEntry.SubmissionId.ShouldBe(submissionId);
        logEntry.BrokerFirmId.ShouldBe($"Acme Brokerage LLC {suffix}");
        logEntry.RequestedCellId.ShouldBe($"cell-outside-panel-{suffix}");
        logEntry.RequestedClassOfBusiness.ShouldBe($"Class-{suffix}");
        logEntry.RejectionReason.ShouldBe($"Panel authorization does not cover requested cell {suffix}");
        logEntry.RejectedAt.ShouldBe(rejectedAt);
        logEntry.ReviewStatus.ShouldBe("Pending");
    }

    // requestedClassOfBusiness is optional on the board (requirements.md Event Model
    // Detail) -- confirms the projector round-trips a null value rather than defaulting
    // it to empty string.
    [Fact]
    public async Task SubmissionRoutingRejected_with_no_requestedClassOfBusiness_leaves_it_null()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var rejectedAt = DateTimeOffset.UtcNow;
        var rejected = new SubmissionRoutingRejected(
            submissionId,
            $"Acme Brokerage LLC {suffix}",
            $"cell-outside-panel-{suffix}",
            null,
            $"Panel authorization does not cover requested cell {suffix}",
            rejectedAt);

        await BrokerAuthorizationExceptionLogProjector.Handle(rejected, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var logEntry = await querySession.LoadAsync<BrokerAuthorizationExceptionLogDoc>(submissionId);

        logEntry.ShouldNotBeNull();
        logEntry.RequestedClassOfBusiness.ShouldBeNull();
    }
}
