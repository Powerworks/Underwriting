using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantCellAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Domain;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AuthorityAdministration.IntegrationTests;

// T010/T011 (retargeted from Layer 2 contract tests to Layer 3 — build-state-change
// SKILL.md's own guidance: FetchForWriting/Query<T> are "awkward to mock
// meaningfully," prefer Testcontainers over fighting IDocumentSession mocks).
[Collection(AuthorityAdministrationPostgresCollection.Name)]
public class GrantCellAuthorityLimitIntegrationTests(AuthorityAdministrationPostgresFixture fixture)
{
    private static AuthorityScope Scope(decimal maxLineSize = 30_000_000, decimal? maxAggregate = 5_000_000) =>
        new(["Property"], "Bermuda", maxLineSize, maxAggregate);

    // T010 — [US1] happy path
    [Fact]
    public async Task Grant_creates_a_new_stream_and_is_readable_via_AuthorityMatrix_in_the_same_session()
    {
        await using var session = fixture.Store.LightweightSession();
        var cellId = $"CELL-{Guid.NewGuid():N}";

        var request = new GrantCellAuthorityLimitRequest(
            "Underwriting Governance", "TFP-CELL-2026", Scope(), "USD",
            DateOnly.FromDateTime(DateTime.UtcNow));

        var result = await GrantCellAuthorityLimitHandler.Handle(cellId, request, session, NullLogger<GrantCellAuthorityLimitHandler>.Instance, CancellationToken.None);

        var created = result.Result.ShouldBeOfType<Created<GrantCellAuthorityLimitResponse>>();
        created.Value!.CellId.ShouldBe(cellId);

        // SC-001 / Clarified 2026-08-09: same-session read-after-write via the
        // Inline snapshot — this is the executable proof the Inline decision exists to satisfy.
        var stored = await session.LoadAsync<AuthorityLimit>(created.Value.AuthorityLimitId);
        stored.ShouldNotBeNull();
        stored.CellId.ShouldBe(cellId);
        stored.Status.ShouldBe("Active");
    }

    // T011 — [US1] Clarified 2026-08-09 / FR-011: duplicate-active-grant rejected
    [Fact]
    public async Task Second_grant_for_the_same_cell_and_class_of_business_is_rejected_while_the_first_is_Active()
    {
        await using var session = fixture.Store.LightweightSession();
        var cellId = $"CELL-{Guid.NewGuid():N}";
        var request = new GrantCellAuthorityLimitRequest(
            "Underwriting Governance", "TFP-CELL-2026", Scope(), "USD",
            DateOnly.FromDateTime(DateTime.UtcNow));

        var first = await GrantCellAuthorityLimitHandler.Handle(cellId, request, session, NullLogger<GrantCellAuthorityLimitHandler>.Instance, CancellationToken.None);
        first.Result.ShouldBeOfType<Created<GrantCellAuthorityLimitResponse>>();

        var second = await GrantCellAuthorityLimitHandler.Handle(cellId, request, session, NullLogger<GrantCellAuthorityLimitHandler>.Instance, CancellationToken.None);

        var problem = second.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(409);
        problem.ProblemDetails.Extensions["rejectionReason"].ShouldBe(AuthorityLimitRejectionReasons.DuplicateActiveGrant);
    }
}
