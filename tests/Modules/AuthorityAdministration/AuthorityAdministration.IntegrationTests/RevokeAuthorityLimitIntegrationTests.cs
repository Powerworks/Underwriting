using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantCellAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.RevokeAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.IntegrationEvents.Published;
using BrokerConnect.Modules.AuthorityAdministration.Domain;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace AuthorityAdministration.IntegrationTests;

// T035-T036 (retargeted from Layer 2 contract tests to Layer 3 — same rationale as
// GrantCellAuthorityLimitIntegrationTests / ReviseAuthorityLimitIntegrationTests).
[Collection(AuthorityAdministrationPostgresCollection.Name)]
public class RevokeAuthorityLimitIntegrationTests(AuthorityAdministrationPostgresFixture fixture)
{
    private static AuthorityScope Scope(decimal maxLineSize = 30_000_000, decimal? maxAggregate = 5_000_000) =>
        new(["Property"], "Bermuda", maxLineSize, maxAggregate);

    private async Task<Guid> GrantActiveCellAsync(string cellId)
    {
        await using var session = fixture.Store.LightweightSession();
        var request = new GrantCellAuthorityLimitRequest(
            "Underwriting Governance", "TFP-CELL-2026", Scope(), "USD",
            DateOnly.FromDateTime(DateTime.UtcNow));
        var result = await GrantCellAuthorityLimitHandler.Handle(cellId, request, session, CancellationToken.None);
        return result.Result.ShouldBeOfType<Created<GrantCellAuthorityLimitResponse>>().Value!.AuthorityLimitId;
    }

    // T035 — [US5] happy path
    [Fact]
    public async Task Revoke_marks_the_limit_Revoked()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        var authorityLimitId = await GrantActiveCellAsync(cellId);
        var bus = Substitute.For<IMessageBus>();

        await using var session = fixture.Store.LightweightSession();
        var request = new RevokeAuthorityLimitRequest("Cell exited the class", "Immediate");

        var result = await RevokeAuthorityLimitHandler.Handle(authorityLimitId, request, session, bus, CancellationToken.None);

        var ok = result.Result.ShouldBeOfType<Ok<RevokeAuthorityLimitResponse>>();
        ok.Value!.AuthorityLimitId.ShouldBe(authorityLimitId);

        var stored = await session.LoadAsync<AuthorityLimit>(authorityLimitId);
        stored!.Status.ShouldBe("Revoked");
    }

    // T036 — [US5] AuthorityLimitChangedV1 published on Revoked (research.md Decision 4),
    // reusing T029's harness (NSubstitute IMessageBus, same rationale as that test).
    [Fact]
    public async Task Revoke_publishes_AuthorityLimitChangedV1()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        var authorityLimitId = await GrantActiveCellAsync(cellId);
        var bus = Substitute.For<IMessageBus>();

        await using var session = fixture.Store.LightweightSession();
        var request = new RevokeAuthorityLimitRequest("Cell exited the class", "Immediate");

        await RevokeAuthorityLimitHandler.Handle(authorityLimitId, request, session, bus, CancellationToken.None);

        await bus.Received(1).PublishAsync(Arg.Is<AuthorityLimitChangedV1>(e =>
            e.AuthorityLimitId == authorityLimitId && e.ChangeType == "Revoked" && e.CellId == cellId));
    }
}
