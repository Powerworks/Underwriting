using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantCellAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantUnderwriterAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.ReviseAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.RevokeAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.IntegrationEvents.Published;
using BrokerConnect.Modules.AuthorityAdministration.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace AuthorityAdministration.IntegrationTests;

// T026-T029 (retargeted from Layer 2 contract tests to Layer 3 — same rationale as
// GrantCellAuthorityLimitIntegrationTests). IMessageBus is substituted (NSubstitute)
// rather than run through a real host: T029's concern is "does the handler hand this
// event to the bus with the right shape," not outbox delivery — there is no consumer
// of AuthorityLimitChangedV1 yet (004-underwriting-decisioning doesn't exist), so
// asserting actual cross-process delivery has nothing to verify against yet.
[Collection(AuthorityAdministrationPostgresCollection.Name)]
public class ReviseAuthorityLimitIntegrationTests(AuthorityAdministrationPostgresFixture fixture)
{
    private static AuthorityScope CellScope(decimal maxLineSize) =>
        new(["Property"], "Bermuda", maxLineSize, 5_000_000);

    private static AuthorityScope UnderwriterScope(decimal maxLineSize) =>
        new(["Property"], "Bermuda", maxLineSize, MaxAggregate: null);

    private async Task<Guid> GrantActiveCellAsync(string cellId, decimal maxLineSize = 30_000_000)
    {
        await using var session = fixture.Store.LightweightSession();
        var request = new GrantCellAuthorityLimitRequest(
            "Underwriting Governance", "TFP-CELL-2026", CellScope(maxLineSize), "USD",
            DateOnly.FromDateTime(DateTime.UtcNow));
        var result = await GrantCellAuthorityLimitHandler.Handle(cellId, request, session, NullLogger<GrantCellAuthorityLimitHandler>.Instance, CancellationToken.None);
        return result.Result.ShouldBeOfType<Created<GrantCellAuthorityLimitResponse>>().Value!.AuthorityLimitId;
    }

    // T026 — [US4] happy path
    [Fact]
    public async Task Revise_an_Active_limit_succeeds_and_increments_the_version()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        var authorityLimitId = await GrantActiveCellAsync(cellId);
        var bus = Substitute.For<IMessageBus>();

        await using var session = fixture.Store.LightweightSession();
        var request = new ReviseAuthorityLimitRequest(20_000_000, 4_000_000, "Treaty renewal adjustment");

        var result = await ReviseAuthorityLimitHandler.Handle(authorityLimitId, request, session, bus, NullLogger<ReviseAuthorityLimitHandler>.Instance, CancellationToken.None);

        var ok = result.Result.ShouldBeOfType<Ok<ReviseAuthorityLimitResponse>>();
        ok.Value!.AuthorityLimitId.ShouldBe(authorityLimitId);
        ok.Value.Version.ShouldBe(1);
    }

    // T027 — [US4] Clarified 2026-08-09 / FR-010: Revoked is terminal
    [Fact]
    public async Task Revising_a_Revoked_limit_is_rejected()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        var authorityLimitId = await GrantActiveCellAsync(cellId);

        await using (var revokeSession = fixture.Store.LightweightSession())
        {
            var revokeBus = Substitute.For<IMessageBus>();
            var revokeResult = await RevokeAuthorityLimitHandler.Handle(
                authorityLimitId, new RevokeAuthorityLimitRequest("Cell exited the class", "Immediate"),
                revokeSession, revokeBus, NullLogger<RevokeAuthorityLimitHandler>.Instance, CancellationToken.None);
            revokeResult.Result.ShouldBeOfType<Ok<RevokeAuthorityLimitResponse>>();
        }

        await using var session = fixture.Store.LightweightSession();
        var bus = Substitute.For<IMessageBus>();
        var request = new ReviseAuthorityLimitRequest(20_000_000, 4_000_000, "Attempted post-revocation edit");

        var result = await ReviseAuthorityLimitHandler.Handle(authorityLimitId, request, session, bus, NullLogger<ReviseAuthorityLimitHandler>.Instance, CancellationToken.None);

        var problem = result.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(409);
        problem.ProblemDetails.Extensions["rejectionReason"].ShouldBe(AuthorityLimitRejectionReasons.RevokedRecord);
    }

    // T028 — [US4] Clarified 2026-08-09 / FR-012: Underwriter-tier revisions re-run the
    // same cascade check as Grant, against the Cell's current limit.
    [Fact]
    public async Task Revising_an_underwriter_limit_beyond_the_cells_current_limit_is_rejected()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        await GrantActiveCellAsync(cellId, maxLineSize: 10_000_000);
        var underwriterId = $"UW-{Guid.NewGuid():N}";

        Guid underwriterAuthorityLimitId;
        await using (var grantSession = fixture.Store.LightweightSession())
        {
            var grantRequest = new GrantUnderwriterAuthorityLimitRequest(UnderwriterScope(5_000_000), "Cell Head");
            var grantResult = await GrantUnderwriterAuthorityLimitHandler.Handle(
                cellId, underwriterId, grantRequest, grantSession, NullLogger<GrantUnderwriterAuthorityLimitHandler>.Instance, CancellationToken.None);
            underwriterAuthorityLimitId = grantResult.Result
                .ShouldBeOfType<Created<GrantUnderwriterAuthorityLimitResponse>>().Value!.AuthorityLimitId;
        }

        await using var session = fixture.Store.LightweightSession();
        var bus = Substitute.For<IMessageBus>();
        var request = new ReviseAuthorityLimitRequest(20_000_000, 2_000_000, "Requesting more than the cell allows");

        var result = await ReviseAuthorityLimitHandler.Handle(
            underwriterAuthorityLimitId, request, session, bus, NullLogger<ReviseAuthorityLimitHandler>.Instance, CancellationToken.None);

        var problem = result.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(409);
        problem.ProblemDetails.Extensions["rejectionReason"].ShouldBe(AuthorityLimitRejectionReasons.ExceedsCellLimit);
    }

    // T029 — [US4] AuthorityLimitChangedV1 published on Revised (research.md Decision 4)
    [Fact]
    public async Task Revise_publishes_AuthorityLimitChangedV1()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        var authorityLimitId = await GrantActiveCellAsync(cellId);
        var bus = Substitute.For<IMessageBus>();

        await using var session = fixture.Store.LightweightSession();
        var request = new ReviseAuthorityLimitRequest(20_000_000, 4_000_000, "Treaty renewal adjustment");

        await ReviseAuthorityLimitHandler.Handle(authorityLimitId, request, session, bus, NullLogger<ReviseAuthorityLimitHandler>.Instance, CancellationToken.None);

        await bus.Received(1).PublishAsync(Arg.Is<AuthorityLimitChangedV1>(e =>
            e.AuthorityLimitId == authorityLimitId && e.ChangeType == "Revised" && e.CellId == cellId));
    }
}
