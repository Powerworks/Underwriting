using BrokerConnect.Modules.AuthorityAdministration.Domain;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Wolverine.Http;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.UnderwriterAuthorityRegister;

public class GetUnderwriterAuthorityRegister
{
    // Same "not specified by source, flagged not guessed" defaults as
    // GetCellAuthorityRegister.
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    [WolverineGet("/api/v1/authority/underwriters/{underwriterId}/register")]
    public static async Task<Results<Ok<UnderwriterAuthorityRegisterResponse>, NotFound>> Handle(
        string underwriterId,
        int? page,
        int? pageSize,
        IQuerySession session,
        ILogger<GetUnderwriterAuthorityRegister> logger,
        CancellationToken cancellationToken)
    {
        var register = await session.LoadAsync<UnderwriterAuthorityRegister>(underwriterId, cancellationToken);
        if (register is null)
        {
            logger.LogDebug("UnderwriterAuthorityRegister not found for underwriter {UnderwriterId}", underwriterId);
            return TypedResults.NotFound();
        }

        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        var orderedHistory = register.History.OrderByDescending(h => h.EffectiveDate).ToList();
        var pagedHistory = orderedHistory
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(h => new UnderwriterAuthorityRegisterHistoryEntryResponse(
                h.Kind, h.SourceId, h.Version, h.Scope, h.EffectiveDate, h.SupersededAt))
            .ToList();

        return TypedResults.Ok(new UnderwriterAuthorityRegisterResponse(
            register.UnderwriterId,
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

public sealed record UnderwriterAuthorityRegisterResponse(
    string UnderwriterId,
    string CellId,
    AuthorityScope? CurrentScope,
    int? CurrentVersion,
    DateOnly? EffectiveDate,
    IReadOnlyList<UnderwriterAuthorityRegisterHistoryEntryResponse> History,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record UnderwriterAuthorityRegisterHistoryEntryResponse(
    string Kind,
    Guid SourceId,
    int? Version,
    AuthorityScope Scope,
    DateOnly EffectiveDate,
    DateTimeOffset? SupersededAt);
