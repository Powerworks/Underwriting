using BrokerConnect.Modules.AuthorityAdministration.Domain;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantCellAuthorityLimit;

public static class GrantCellAuthorityLimitHandler
{
    // TODO(ADR-010): [Authorize] policy deferred until the identity-provider
    // decision (Solution Arch §8, DEC-032) lands — no policy name exists yet to
    // reference without guessing.
    [WolverinePost("/api/v1/authority/cells/{cellId}/limits")]
    public static async Task<Results<Created<GrantCellAuthorityLimitResponse>, ProblemHttpResult>> Handle(
        string cellId,
        GrantCellAuthorityLimitRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        // Clarified 2026-08-09 / FR-011: at most one Active Cell-tier grant per
        // (cellId, classOfBusiness). Queries the AuthorityLimit Inline snapshot
        // (session.Query<AuthorityLimit>()) — compiles against any Marten document
        // type now; becomes functionally live once the snapshot projection is
        // registered (tasks.md T046, US2). In-memory overlap check rather than a
        // translated JSONB LINQ query, to stay on well-trodden Marten LINQ ground.
        var activeCellGrants = await session.Query<AuthorityLimit>()
            .Where(a => a.Tier == "Cell" && a.CellId == cellId && a.Status == "Active")
            .ToListAsync(cancellationToken);

        var duplicate = activeCellGrants.FirstOrDefault(a =>
            a.Scope.ClassesOfBusiness.Any(request.AuthorityScope.ClassesOfBusiness.Contains));

        if (duplicate is not null)
        {
            var rejected = new CellAuthorityLimitGrantRejected(
                duplicate.AuthorityLimitId,
                cellId,
                request.AuthorityScope,
                AuthorityLimitRejectionReasons.DuplicateActiveGrant,
                AttemptedBy: request.GrantingParty,
                RejectedAt: DateTimeOffset.UtcNow);

            session.Events.Append(duplicate.AuthorityLimitId, rejected);
            await session.SaveChangesAsync(cancellationToken);

            return AuthorityRejectionProblem.Conflict(
                duplicate.AuthorityLimitId, rejected.RejectionReason,
                $"An Active Cell-tier authority limit already exists for cell '{cellId}' covering the requested class(es) of business.");
        }

        var authorityLimitId = Guid.NewGuid();
        var granted = new CellAuthorityLimitGranted(
            authorityLimitId,
            cellId,
            request.AuthorityScope,
            request.SourceAgreementReference,
            GrantedBy: request.GrantingParty,
            request.EffectiveDate,
            Version: 0,
            GrantedAt: DateTimeOffset.UtcNow);

        session.Events.StartStream<AuthorityLimit>(authorityLimitId, granted);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v1/authority/matrix/{authorityLimitId}",
            new GrantCellAuthorityLimitResponse(authorityLimitId, cellId, granted.Version, granted.GrantedAt));
    }
}
