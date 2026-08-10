using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantCellAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.AuthorityMatrix;
using BrokerConnect.Modules.AuthorityAdministration.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;
using Xunit;

namespace AuthorityAdministration.IntegrationTests;

// T045 — [US2] executable proof of SC-001 / Clarified 2026-08-09: the AuthorityMatrix
// read model (Inline snapshot, T046) reflects a grant within the same session,
// not just eventually. Same Layer 3 rationale as the other *IntegrationTests files.
[Collection(AuthorityAdministrationPostgresCollection.Name)]
public class AuthorityMatrixInlineSnapshotTests(AuthorityAdministrationPostgresFixture fixture)
{
    private static AuthorityScope Scope(decimal maxLineSize = 30_000_000, decimal? maxAggregate = 5_000_000) =>
        new(["Property"], "Bermuda", maxLineSize, maxAggregate);

    [Fact]
    public async Task Grant_is_visible_via_AuthorityMatrix_in_the_same_session_immediately()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        await using var session = fixture.Store.LightweightSession();

        var grantRequest = new GrantCellAuthorityLimitRequest(
            "Underwriting Governance", "TFP-CELL-2026", Scope(), "USD",
            DateOnly.FromDateTime(DateTime.UtcNow));
        var grantResult = await GrantCellAuthorityLimitHandler.Handle(cellId, grantRequest, session, CancellationToken.None);
        var authorityLimitId = grantResult.Result
            .ShouldBeOfType<Created<GrantCellAuthorityLimitResponse>>().Value!.AuthorityLimitId;

        var matrixResult = await GetAuthorityMatrix.Handle(authorityLimitId, session, CancellationToken.None);

        var ok = matrixResult.Result.ShouldBeOfType<Ok<AuthorityMatrixResponse>>();
        ok.Value!.AuthorityLimitId.ShouldBe(authorityLimitId);
        ok.Value.CellId.ShouldBe(cellId);
        ok.Value.Tier.ShouldBe("Cell");
        ok.Value.Status.ShouldBe("Active");
        ok.Value.Version.ShouldBe(0);
    }

    [Fact]
    public async Task Unknown_id_returns_NotFound()
    {
        await using var session = fixture.Store.LightweightSession();

        var result = await GetAuthorityMatrix.Handle(Guid.NewGuid(), session, CancellationToken.None);

        result.Result.ShouldBeOfType<NotFound>();
    }
}
