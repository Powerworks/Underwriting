using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantCellAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantUnderwriterAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.RequestCellAuthorityIncrease;
using BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.UnderwriterAuthorityRegister;
using BrokerConnect.Modules.AuthorityAdministration.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;
using Xunit;

namespace AuthorityAdministration.IntegrationTests;

// T052 [US8] — same Layer 3 rationale as CellAuthorityRegisterProjectionTests
// (Async-lifecycle projection, needs the real Testcontainers Postgres + daemon).
[Collection(AuthorityAdministrationPostgresCollection.Name)]
public class UnderwriterAuthorityRegisterProjectionTests(AuthorityAdministrationPostgresFixture fixture)
{
    private static AuthorityScope CellScope(decimal maxLineSize = 30_000_000, decimal? maxAggregate = 5_000_000) =>
        new(["Property"], "Bermuda", maxLineSize, maxAggregate);

    private static AuthorityScope UnderwriterScope(decimal maxLineSize) =>
        new(["Property"], "Bermuda", maxLineSize, MaxAggregate: null);

    private async Task<Guid> GrantActiveCellAsync(string cellId)
    {
        await using var session = fixture.Store.LightweightSession();
        var request = new GrantCellAuthorityLimitRequest(
            "Underwriting Governance", "TFP-CELL-2026", CellScope(), "USD",
            DateOnly.FromDateTime(DateTime.UtcNow));
        var result = await GrantCellAuthorityLimitHandler.Handle(cellId, request, session, CancellationToken.None);
        return result.Result.ShouldBeOfType<Created<GrantCellAuthorityLimitResponse>>().Value!.AuthorityLimitId;
    }

    // T052 — grant reflected
    [Fact]
    public async Task Grant_is_reflected_in_the_underwriters_register_after_the_daemon_catches_up()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        var underwriterId = $"UW-{Guid.NewGuid():N}";
        await GrantActiveCellAsync(cellId);

        Guid authorityLimitId;
        await using (var session = fixture.Store.LightweightSession())
        {
            var request = new GrantUnderwriterAuthorityLimitRequest(UnderwriterScope(5_000_000), "Cell Head");
            var result = await GrantUnderwriterAuthorityLimitHandler.Handle(
                cellId, underwriterId, request, session, CancellationToken.None);
            authorityLimitId = result.Result
                .ShouldBeOfType<Created<GrantUnderwriterAuthorityLimitResponse>>().Value!.AuthorityLimitId;
        }

        await fixture.WaitForProjectionsAsync();

        await using var query = fixture.Store.QuerySession();
        var register = await query.LoadAsync<UnderwriterAuthorityRegister>(underwriterId);

        register.ShouldNotBeNull();
        register.CellId.ShouldBe(cellId);
        register.CurrentVersion.ShouldBe(0);
        register.History.Count.ShouldBe(1);
        register.History[0].Kind.ShouldBe("Grant");
        register.History[0].SourceId.ShouldBe(authorityLimitId);
    }

    // T052 — an increase request can be the first-ever event for an underwriter
    // (it follows a *rejected* grant, so there may be no prior UnderwriterAuthorityLimitGranted)
    [Fact]
    public async Task Increase_request_with_no_prior_grant_leaves_current_authority_null()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        var underwriterId = $"UW-{Guid.NewGuid():N}";
        var cellAuthorityLimitId = await GrantActiveCellAsync(cellId);

        await using (var session = fixture.Store.LightweightSession())
        {
            var request = new RequestCellAuthorityIncreaseRequest(
                underwriterId, UnderwriterScope(20_000_000), "Requesting more than the cell allows");
            var result = await RequestCellAuthorityIncreaseHandler.Handle(
                cellAuthorityLimitId, request, session, CancellationToken.None);
            result.Result.ShouldBeOfType<Created<RequestCellAuthorityIncreaseResponse>>();
        }

        await fixture.WaitForProjectionsAsync();

        await using var query = fixture.Store.QuerySession();
        var register = await query.LoadAsync<UnderwriterAuthorityRegister>(underwriterId);

        register.ShouldNotBeNull();
        register.CellId.ShouldBe(cellId);
        register.CurrentScope.ShouldBeNull();
        register.CurrentVersion.ShouldBeNull();
        register.History.Count.ShouldBe(1);
        register.History[0].Kind.ShouldBe("IncreaseRequested");
        register.History[0].Version.ShouldBeNull();
    }

    // T052 — an increase request after a grant appends history without touching
    // the grant's current authority
    [Fact]
    public async Task Increase_request_after_a_grant_does_not_change_current_authority()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        var underwriterId = $"UW-{Guid.NewGuid():N}";
        var cellAuthorityLimitId = await GrantActiveCellAsync(cellId);

        await using (var session = fixture.Store.LightweightSession())
        {
            var grantRequest = new GrantUnderwriterAuthorityLimitRequest(UnderwriterScope(5_000_000), "Cell Head");
            await GrantUnderwriterAuthorityLimitHandler.Handle(cellId, underwriterId, grantRequest, session, CancellationToken.None);
        }

        await using (var session = fixture.Store.LightweightSession())
        {
            var increaseRequest = new RequestCellAuthorityIncreaseRequest(
                underwriterId, UnderwriterScope(20_000_000), "Growing book requires higher line size");
            await RequestCellAuthorityIncreaseHandler.Handle(cellAuthorityLimitId, increaseRequest, session, CancellationToken.None);
        }

        await fixture.WaitForProjectionsAsync();

        await using var query = fixture.Store.QuerySession();
        var register = await query.LoadAsync<UnderwriterAuthorityRegister>(underwriterId);

        register.ShouldNotBeNull();
        register.CurrentVersion.ShouldBe(0); // unchanged by the request
        register.History.Count.ShouldBe(2);
        register.History.ShouldContain(h => h.Kind == "Grant");
        register.History.ShouldContain(h => h.Kind == "IncreaseRequested");
    }

    // T054 endpoint coverage — pagination
    [Fact]
    public async Task GetUnderwriterAuthorityRegister_paginates_history_newest_first()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        var underwriterId = $"UW-{Guid.NewGuid():N}";
        var cellAuthorityLimitId = await GrantActiveCellAsync(cellId);

        await using (var session = fixture.Store.LightweightSession())
        {
            var grantRequest = new GrantUnderwriterAuthorityLimitRequest(UnderwriterScope(5_000_000), "Cell Head");
            await GrantUnderwriterAuthorityLimitHandler.Handle(cellId, underwriterId, grantRequest, session, CancellationToken.None);
        }

        foreach (var justification in new[] { "First ask", "Second ask" })
        {
            await using var session = fixture.Store.LightweightSession();
            var increaseRequest = new RequestCellAuthorityIncreaseRequest(underwriterId, UnderwriterScope(20_000_000), justification);
            await RequestCellAuthorityIncreaseHandler.Handle(cellAuthorityLimitId, increaseRequest, session, CancellationToken.None);
        }

        await fixture.WaitForProjectionsAsync();

        await using var query = fixture.Store.QuerySession();
        var firstPage = await GetUnderwriterAuthorityRegister.Handle(underwriterId, page: 1, pageSize: 2, query, CancellationToken.None);
        var firstOk = firstPage.Result.ShouldBeOfType<Ok<UnderwriterAuthorityRegisterResponse>>();
        firstOk.Value!.History.Count.ShouldBe(2);
        firstOk.Value.TotalCount.ShouldBe(3); // 1 grant + 2 increase requests

        var secondPage = await GetUnderwriterAuthorityRegister.Handle(underwriterId, page: 2, pageSize: 2, query, CancellationToken.None);
        var secondOk = secondPage.Result.ShouldBeOfType<Ok<UnderwriterAuthorityRegisterResponse>>();
        secondOk.Value!.History.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetUnderwriterAuthorityRegister_returns_NotFound_for_an_underwriter_with_no_history()
    {
        await using var query = fixture.Store.QuerySession();

        var result = await GetUnderwriterAuthorityRegister.Handle(
            $"UW-{Guid.NewGuid():N}", page: null, pageSize: null, query, CancellationToken.None);

        result.Result.ShouldBeOfType<NotFound>();
    }
}
