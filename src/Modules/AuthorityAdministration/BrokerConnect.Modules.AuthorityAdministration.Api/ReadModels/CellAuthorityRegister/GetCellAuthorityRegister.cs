using BrokerConnect.Modules.AuthorityAdministration.Domain;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.CellAuthorityRegister;

public static class GetCellAuthorityRegister
{
    // Contracts doc: "v1 default page size TBD, not specified by source — flag at
    // implementation time." Flagging here: 20 is a guess, not a transcribed value —
    // caller-overridable via ?pageSize, capped to avoid an unbounded response.
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    [WolverineGet("/api/v1/authority/cells/{cellId}/register")]
    public static async Task<Results<Ok<CellAuthorityRegisterResponse>, NotFound>> Handle(
        string cellId,
        int? page,
        int? pageSize,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var register = await session.LoadAsync<CellAuthorityRegister>(cellId, cancellationToken);
        if (register is null)
            return TypedResults.NotFound();

        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        // Newest first — a register is read to answer "what changed and when,"
        // most-recent history is what a caller wants without paging through everything.
        var orderedHistory = register.History.OrderByDescending(h => h.EffectiveDate).ToList();
        var pagedHistory = orderedHistory
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(h => new CellAuthorityRegisterHistoryEntryResponse(
                h.AuthorityLimitId, h.Version, h.Scope, h.EffectiveDate, h.SupersededAt))
            .ToList();

        return TypedResults.Ok(new CellAuthorityRegisterResponse(
            register.CellId,
            register.CurrentScope,
            register.CurrentVersion,
            register.EffectiveDate,
            pagedHistory,
            effectivePage,
            effectivePageSize,
            TotalCount: orderedHistory.Count));
    }
}

public sealed record CellAuthorityRegisterResponse(
    string CellId,
    AuthorityScope CurrentScope,
    int CurrentVersion,
    DateOnly EffectiveDate,
    IReadOnlyList<CellAuthorityRegisterHistoryEntryResponse> History,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CellAuthorityRegisterHistoryEntryResponse(
    Guid AuthorityLimitId,
    int Version,
    AuthorityScope Scope,
    DateOnly EffectiveDate,
    DateTimeOffset? SupersededAt);
