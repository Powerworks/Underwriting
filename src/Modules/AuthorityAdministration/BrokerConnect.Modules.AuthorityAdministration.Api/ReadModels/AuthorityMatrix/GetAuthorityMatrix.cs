using BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.AuthorityMatrix;

public static class GetAuthorityMatrix
{
    // T046/T047 (US2, Clarified 2026-08-09 / SC-001): reads the Inline snapshot
    // registered in Module.cs — same-session read-after-write is what makes
    // T016/T025/T033's duplicate/cascade checks (also querying this snapshot,
    // see T048) reliable. Constitution Principle VII: returns a DTO, never the
    // AuthorityLimit aggregate itself.
    [WolverineGet("/api/v1/authority/matrix/{authorityLimitId}")]
    public static async Task<Results<Ok<AuthorityMatrixResponse>, NotFound>> Handle(
        Guid authorityLimitId,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var entity = await session.LoadAsync<AuthorityLimit>(authorityLimitId, cancellationToken);
        if (entity is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new AuthorityMatrixResponse(
            entity.AuthorityLimitId,
            entity.Tier,
            entity.CellId,
            entity.UnderwriterId,
            entity.Scope.ClassesOfBusiness,
            entity.Scope.Territory,
            entity.Scope.MaxLineSize,
            entity.Scope.MaxAggregate,
            entity.SourceAgreementReference,
            entity.Status,
            entity.RevisionNumber,
            entity.GrantedBy,
            entity.GrantedAt));
    }
}

// data-model.md's AuthorityMatrix shape also lists `providerId` and `currency` —
// neither is actually persisted anywhere upstream of this read model (not on
// AuthorityLimit, not on either Granted event; GrantCellAuthorityLimitRequest
// accepts a Currency field that is validated but never stored). Flagging the gap
// here rather than inventing a value: this DTO exposes only what the aggregate
// actually carries today.
public sealed record AuthorityMatrixResponse(
    Guid AuthorityLimitId,
    string Tier,
    string CellId,
    string? UnderwriterId,
    IReadOnlyList<string> ClassesOfBusiness,
    string Territory,
    decimal MaxGrossPremium,
    decimal? MaxLimit,
    string? SourceAgreementReference,
    string Status,
    int Version,
    string GrantedBy,
    DateTimeOffset GrantedAt);
