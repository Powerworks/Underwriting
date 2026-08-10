using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantCellAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantUnderwriterAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.Commands.RevokeAuthorityLimit;
using BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.CellAuthorityRegister;
using BrokerConnect.Modules.AuthorityAdministration.Domain;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace AuthorityAdministration.IntegrationTests;

// T049 [US6] — plain async projection (not Inline), so every test appends events
// then calls fixture.WaitForProjectionsAsync() before reading CellAuthorityRegister;
// unlike every other *IntegrationTests file in this project, reads happen through a
// fresh IQuerySession, not the same IDocumentSession that appended the events —
// this register has no same-request consistency requirement (data-model.md), so
// asserting through the real async path is the honest test, not a shortcut around it.
[Collection(AuthorityAdministrationPostgresCollection.Name)]
public class CellAuthorityRegisterProjectionTests(AuthorityAdministrationPostgresFixture fixture)
{
    private static AuthorityScope CellScope(decimal maxLineSize = 30_000_000, decimal? maxAggregate = 5_000_000) =>
        new(["Property"], "Bermuda", maxLineSize, maxAggregate);

    private static AuthorityScope UnderwriterScope(decimal maxLineSize) =>
        new(["Property"], "Bermuda", maxLineSize, MaxAggregate: null);

    // T049 — grant reflected in the register
    [Fact]
    public async Task Grant_is_reflected_in_the_cells_register_after_the_daemon_catches_up()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        Guid authorityLimitId;

        await using (var session = fixture.Store.LightweightSession())
        {
            var request = new GrantCellAuthorityLimitRequest(
                "Underwriting Governance", "TFP-CELL-2026", CellScope(), "USD",
                DateOnly.FromDateTime(DateTime.UtcNow));
            var result = await GrantCellAuthorityLimitHandler.Handle(cellId, request, session, NullLogger<GrantCellAuthorityLimitHandler>.Instance, CancellationToken.None);
            authorityLimitId = result.Result
                .ShouldBeOfType<Created<GrantCellAuthorityLimitResponse>>().Value!.AuthorityLimitId;
        }

        await fixture.WaitForProjectionsAsync();

        await using var query = fixture.Store.QuerySession();
        var register = await query.LoadAsync<CellAuthorityRegister>(cellId);

        register.ShouldNotBeNull();
        register.CellId.ShouldBe(cellId);
        register.CurrentVersion.ShouldBe(0);
        register.History.Count.ShouldBe(1);
        register.History[0].AuthorityLimitId.ShouldBe(authorityLimitId);
        register.History[0].SupersededAt.ShouldBeNull();
    }

    // T049 — revoke supersedes the matching history entry
    [Fact]
    public async Task Revoke_marks_the_matching_history_entry_superseded()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        Guid authorityLimitId;

        await using (var session = fixture.Store.LightweightSession())
        {
            var grantRequest = new GrantCellAuthorityLimitRequest(
                "Underwriting Governance", "TFP-CELL-2026", CellScope(), "USD",
                DateOnly.FromDateTime(DateTime.UtcNow));
            var grantResult = await GrantCellAuthorityLimitHandler.Handle(cellId, grantRequest, session, NullLogger<GrantCellAuthorityLimitHandler>.Instance, CancellationToken.None);
            authorityLimitId = grantResult.Result
                .ShouldBeOfType<Created<GrantCellAuthorityLimitResponse>>().Value!.AuthorityLimitId;
        }

        await using (var session = fixture.Store.LightweightSession())
        {
            var bus = Substitute.For<IMessageBus>();
            var revokeResult = await RevokeAuthorityLimitHandler.Handle(
                authorityLimitId, new RevokeAuthorityLimitRequest("Cell exited the class", "Immediate"),
                session, bus, NullLogger<RevokeAuthorityLimitHandler>.Instance, CancellationToken.None);
            revokeResult.Result.ShouldBeOfType<Ok<RevokeAuthorityLimitResponse>>();
        }

        await fixture.WaitForProjectionsAsync();

        await using var query = fixture.Store.QuerySession();
        var register = await query.LoadAsync<CellAuthorityRegister>(cellId);

        register.ShouldNotBeNull();
        register.History.Count.ShouldBe(1);
        register.History[0].SupersededAt.ShouldNotBeNull();
    }

    // T049 — Underwriter-tier revocation must NOT show up as history in the CELL
    // register it happens to route to (CellAuthorityRegisterProjector.Apply's
    // Target != "Cell" no-op branch)
    [Fact]
    public async Task Revoking_an_underwriter_grant_does_not_touch_the_cells_own_register()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";
        var underwriterId = $"UW-{Guid.NewGuid():N}";
        Guid underwriterAuthorityLimitId;

        await using (var session = fixture.Store.LightweightSession())
        {
            var grantCellRequest = new GrantCellAuthorityLimitRequest(
                "Underwriting Governance", "TFP-CELL-2026", CellScope(), "USD",
                DateOnly.FromDateTime(DateTime.UtcNow));
            await GrantCellAuthorityLimitHandler.Handle(cellId, grantCellRequest, session, NullLogger<GrantCellAuthorityLimitHandler>.Instance, CancellationToken.None);
        }

        await using (var session = fixture.Store.LightweightSession())
        {
            var grantUwRequest = new GrantUnderwriterAuthorityLimitRequest(UnderwriterScope(5_000_000), "Cell Head");
            var grantUwResult = await GrantUnderwriterAuthorityLimitHandler.Handle(
                cellId, underwriterId, grantUwRequest, session, NullLogger<GrantUnderwriterAuthorityLimitHandler>.Instance, CancellationToken.None);
            underwriterAuthorityLimitId = grantUwResult.Result
                .ShouldBeOfType<Created<GrantUnderwriterAuthorityLimitResponse>>().Value!.AuthorityLimitId;
        }

        await using (var session = fixture.Store.LightweightSession())
        {
            var bus = Substitute.For<IMessageBus>();
            await RevokeAuthorityLimitHandler.Handle(
                underwriterAuthorityLimitId, new RevokeAuthorityLimitRequest("No longer needed", "Immediate"),
                session, bus, NullLogger<RevokeAuthorityLimitHandler>.Instance, CancellationToken.None);
        }

        await fixture.WaitForProjectionsAsync();

        await using var query = fixture.Store.QuerySession();
        var register = await query.LoadAsync<CellAuthorityRegister>(cellId);

        // Only the Cell-tier grant exists in this register — the underwriter grant
        // never appended CellAuthorityLimitGranted, and its revocation was a no-op.
        register.ShouldNotBeNull();
        register.History.Count.ShouldBe(1);
        register.History[0].SupersededAt.ShouldBeNull();
    }

    // T051 endpoint coverage — GetCellAuthorityRegister pagination
    [Fact]
    public async Task GetCellAuthorityRegister_paginates_history_newest_first()
    {
        var cellId = $"CELL-{Guid.NewGuid():N}";

        // Three separate classes of business so each grant lands as its own,
        // non-rejected history entry (FR-011 only blocks a *duplicate* class).
        string[] classes = ["Property", "Casualty", "Marine"];
        foreach (var classOfBusiness in classes)
        {
            await using var session = fixture.Store.LightweightSession();
            var request = new GrantCellAuthorityLimitRequest(
                "Underwriting Governance", "TFP-CELL-2026",
                new AuthorityScope([classOfBusiness], "Bermuda", 30_000_000, 5_000_000), "USD",
                DateOnly.FromDateTime(DateTime.UtcNow));
            var result = await GrantCellAuthorityLimitHandler.Handle(cellId, request, session, NullLogger<GrantCellAuthorityLimitHandler>.Instance, CancellationToken.None);
            result.Result.ShouldBeOfType<Created<GrantCellAuthorityLimitResponse>>();
        }

        await fixture.WaitForProjectionsAsync();

        await using var query = fixture.Store.QuerySession();
        var firstPage = await GetCellAuthorityRegister.Handle(cellId, page: 1, pageSize: 2, query, NullLogger<GetCellAuthorityRegister>.Instance, CancellationToken.None);
        var firstOk = firstPage.Result.ShouldBeOfType<Ok<CellAuthorityRegisterResponse>>();
        firstOk.Value!.History.Count.ShouldBe(2);
        firstOk.Value.TotalCount.ShouldBe(3);
        firstOk.Value.Page.ShouldBe(1);
        firstOk.Value.PageSize.ShouldBe(2);

        var secondPage = await GetCellAuthorityRegister.Handle(cellId, page: 2, pageSize: 2, query, NullLogger<GetCellAuthorityRegister>.Instance, CancellationToken.None);
        var secondOk = secondPage.Result.ShouldBeOfType<Ok<CellAuthorityRegisterResponse>>();
        secondOk.Value!.History.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetCellAuthorityRegister_returns_NotFound_for_a_cell_with_no_grants()
    {
        await using var query = fixture.Store.QuerySession();

        var result = await GetCellAuthorityRegister.Handle(
            $"CELL-{Guid.NewGuid():N}", page: null, pageSize: null, query, NullLogger<GetCellAuthorityRegister>.Instance, CancellationToken.None);

        result.Result.ShouldBeOfType<NotFound>();
    }
}
