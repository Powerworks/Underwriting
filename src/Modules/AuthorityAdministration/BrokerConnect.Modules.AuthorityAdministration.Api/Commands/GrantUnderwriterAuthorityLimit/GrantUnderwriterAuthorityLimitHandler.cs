using BrokerConnect.Modules.AuthorityAdministration.Domain;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Wolverine.Http;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantUnderwriterAuthorityLimit;

public class GrantUnderwriterAuthorityLimitHandler
{
    [WolverinePost("/api/v1/authority/cells/{cellId}/underwriters/{underwriterId}/limits")]
    public static async Task<Results<Created<GrantUnderwriterAuthorityLimitResponse>, NotFound, ProblemHttpResult>> Handle(
        string cellId,
        string underwriterId,
        GrantUnderwriterAuthorityLimitRequest request,
        IDocumentSession session,
        ILogger<GrantUnderwriterAuthorityLimitHandler> logger,
        CancellationToken cancellationToken)
    {
        var cellGrant = await session.Query<AuthorityLimit>()
            .Where(a => a.Tier == "Cell" && a.CellId == cellId && a.Status == "Active")
            .FirstOrDefaultAsync(cancellationToken);

        if (cellGrant is null)
        {
            logger.LogInformation(
                "Underwriter authority limit grant for underwriter {UnderwriterId} rejected: no Active Cell-tier grant found for cell {CellId}",
                underwriterId, cellId);
            return TypedResults.NotFound();
        }

        // FR-003/FR-006: cascade check against the Cell's own current limit.
        var exceedsCellLimit = request.RequestedScope.MaxLineSize > cellGrant.Scope.MaxLineSize
            || !request.RequestedScope.ClassesOfBusiness.All(cellGrant.Scope.ClassesOfBusiness.Contains);

        if (exceedsCellLimit)
        {
            await RejectAsync(session, logger, cellGrant.AuthorityLimitId, underwriterId, cellId, request,
                AuthorityLimitRejectionReasons.ExceedsCellLimit, cancellationToken);
            return AuthorityRejectionProblem.Conflict(
                cellGrant.AuthorityLimitId, AuthorityLimitRejectionReasons.ExceedsCellLimit,
                $"Requested limit exceeds cell '{cellId}''s own current delegated authority.");
        }

        // Clarified 2026-08-09 / FR-011: at most one Active Underwriter-tier grant
        // per (underwriterId, classOfBusiness) — same duplicate-active rule as the
        // Cell tier (US1), scoped to this underwriter.
        var existingUnderwriterGrants = await session.Query<AuthorityLimit>()
            .Where(a => a.Tier == "Underwriter" && a.UnderwriterId == underwriterId && a.Status == "Active")
            .ToListAsync(cancellationToken);

        var duplicate = existingUnderwriterGrants.Any(a =>
            a.Scope.ClassesOfBusiness.Any(request.RequestedScope.ClassesOfBusiness.Contains));

        if (duplicate)
        {
            await RejectAsync(session, logger, cellGrant.AuthorityLimitId, underwriterId, cellId, request,
                AuthorityLimitRejectionReasons.DuplicateActiveGrant, cancellationToken);
            return AuthorityRejectionProblem.Conflict(
                cellGrant.AuthorityLimitId, AuthorityLimitRejectionReasons.DuplicateActiveGrant,
                $"An Active Underwriter-tier authority limit already exists for underwriter '{underwriterId}' covering the requested class(es) of business.");
        }

        var authorityLimitId = Guid.NewGuid();
        var granted = new UnderwriterAuthorityLimitGranted(
            authorityLimitId,
            underwriterId,
            cellId,
            request.RequestedScope,
            GrantedBy: request.GrantingAuthority,
            EffectiveDate: DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date),
            Version: 0,
            ValidationResult: "WithinCellLimit",
            GrantedAt: DateTimeOffset.UtcNow);

        session.Events.StartStream<AuthorityLimit>(authorityLimitId, granted);
        await session.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Underwriter authority limit {AuthorityLimitId} granted for underwriter {UnderwriterId} under cell {CellId}",
            authorityLimitId, underwriterId, cellId);

        return TypedResults.Created(
            $"/api/v1/authority/matrix/{authorityLimitId}",
            new GrantUnderwriterAuthorityLimitResponse(
                authorityLimitId, underwriterId, cellId, granted.Version, granted.ValidationResult, granted.GrantedAt));
    }

    private static async Task RejectAsync(
        IDocumentSession session, ILogger logger, Guid cellAuthorityLimitId, string underwriterId, string cellId,
        GrantUnderwriterAuthorityLimitRequest request, string rejectionReason, CancellationToken cancellationToken)
    {
        // Appends to the Cell's own stream — the entity the rejection was
        // evaluated against (data-model.md Decision 3), not a new stream.
        var rejected = new UnderwriterAuthorityLimitRejected(
            cellAuthorityLimitId, underwriterId, cellId, request.RequestedScope,
            rejectionReason, AttemptedBy: request.GrantingAuthority, RejectedAt: DateTimeOffset.UtcNow);

        session.Events.Append(cellAuthorityLimitId, rejected);
        await session.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Underwriter authority limit grant rejected for underwriter {UnderwriterId} under cell {CellId}: {RejectionReason}",
            underwriterId, cellId, rejectionReason);
    }
}
