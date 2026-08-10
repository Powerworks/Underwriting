using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantCellAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantUnderwriterAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AuthorityAdministration.IntegrationTests;

// T018-T020 (retargeted from Layer 2 contract tests to Layer 3 — same rationale as
// GrantCellAuthorityLimitIntegrationTests: session.Query<AuthorityLimit> is awkward
// to mock meaningfully, so these run against a real Postgres via Testcontainers).
[Collection(AuthorityAdministrationPostgresCollection.Name)]
public class GrantUnderwriterAuthorityLimitIntegrationTests(AuthorityAdministrationPostgresFixture fixture)
{
    private static AuthorityScope CellScope(decimal maxLineSize) =>
        new(["Property"], "Bermuda", maxLineSize, 5_000_000);

    private static AuthorityScope UnderwriterScope(decimal maxLineSize) =>
        new(["Property"], "Bermuda", maxLineSize, MaxAggregate: null);

    private async Task GrantActiveCellAsync(string cellId, decimal cellMaxLineSize)
    {
        await using var session = fixture.Store.LightweightSession();
        var request = new GrantCellAuthorityLimitRequest(
            "Underwriting Governance", "TFP-CELL-2026", CellScope(cellMaxLineSize), "USD",
            DateOnly.FromDateTime(DateTime.UtcNow));
        var result = await GrantCellAuthorityLimitHandler.Handle(cellId, request, session, NullLogger<GrantCellAuthorityLimitHandler>.Instance, CancellationToken.None);
        result.Result.ShouldBeOfType<Created<GrantCellAuthorityLimitResponse>>();
    }

    // T018 — [US3] happy path
    [Fact]
    public async Task Grant_within_the_cells_current_limit_succeeds_and_is_marked_WithinCellLimit()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        await GrantActiveCellAsync(cellId, cellMaxLineSize: 30_000_000);
        var underwriterId = $"UW-{Guid.NewGuid():N}";

        await using var session = fixture.Store.LightweightSession();
        var request = new GrantUnderwriterAuthorityLimitRequest(UnderwriterScope(5_000_000), "Cell Head");

        var result = await GrantUnderwriterAuthorityLimitHandler.Handle(
            cellId, underwriterId, request, session, NullLogger<GrantUnderwriterAuthorityLimitHandler>.Instance, CancellationToken.None);

        var created = result.Result.ShouldBeOfType<Created<GrantUnderwriterAuthorityLimitResponse>>();
        created.Value!.UnderwriterId.ShouldBe(underwriterId);
        created.Value.CellId.ShouldBe(cellId);
        created.Value.ValidationResult.ShouldBe("WithinCellLimit");
    }

    // T019 — [US3] FR-003/FR-006: exceeds the cell's own current limit
    [Fact]
    public async Task Grant_exceeding_the_cells_current_limit_is_rejected()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        await GrantActiveCellAsync(cellId, cellMaxLineSize: 10_000_000);
        var underwriterId = $"UW-{Guid.NewGuid():N}";

        await using var session = fixture.Store.LightweightSession();
        var request = new GrantUnderwriterAuthorityLimitRequest(UnderwriterScope(20_000_000), "Cell Head");

        var result = await GrantUnderwriterAuthorityLimitHandler.Handle(
            cellId, underwriterId, request, session, NullLogger<GrantUnderwriterAuthorityLimitHandler>.Instance, CancellationToken.None);

        var problem = result.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(409);
        problem.ProblemDetails.Extensions["rejectionReason"].ShouldBe(AuthorityLimitRejectionReasons.ExceedsCellLimit);
    }

    // T020 — [US3] Clarified 2026-08-09 / FR-011: duplicate-active-grant rejected
    [Fact]
    public async Task Second_grant_for_the_same_underwriter_and_class_of_business_is_rejected_while_the_first_is_Active()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        await GrantActiveCellAsync(cellId, cellMaxLineSize: 30_000_000);
        var underwriterId = $"UW-{Guid.NewGuid():N}";
        var request = new GrantUnderwriterAuthorityLimitRequest(UnderwriterScope(5_000_000), "Cell Head");

        await using var first = fixture.Store.LightweightSession();
        var firstResult = await GrantUnderwriterAuthorityLimitHandler.Handle(
            cellId, underwriterId, request, first, NullLogger<GrantUnderwriterAuthorityLimitHandler>.Instance, CancellationToken.None);
        firstResult.Result.ShouldBeOfType<Created<GrantUnderwriterAuthorityLimitResponse>>();

        await using var second = fixture.Store.LightweightSession();
        var secondResult = await GrantUnderwriterAuthorityLimitHandler.Handle(
            cellId, underwriterId, request, second, NullLogger<GrantUnderwriterAuthorityLimitHandler>.Instance, CancellationToken.None);

        var problem = secondResult.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(409);
        problem.ProblemDetails.Extensions["rejectionReason"].ShouldBe(AuthorityLimitRejectionReasons.DuplicateActiveGrant);
    }
}
