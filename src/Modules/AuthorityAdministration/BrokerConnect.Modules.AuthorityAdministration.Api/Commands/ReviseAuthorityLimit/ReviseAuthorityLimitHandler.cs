using BrokerConnect.Modules.AuthorityAdministration.Api.IntegrationEvents.Published;
using BrokerConnect.Modules.AuthorityAdministration.Domain;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Http;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.Commands.ReviseAuthorityLimit;

public class ReviseAuthorityLimitHandler
{
    [WolverinePut("/api/v1/authority/limits/{authorityLimitId}/revision")]
    public static async Task<Results<Ok<ReviseAuthorityLimitResponse>, NotFound, ProblemHttpResult>> Handle(
        Guid authorityLimitId,
        ReviseAuthorityLimitRequest request,
        IDocumentSession session,
        IMessageBus bus,
        ILogger<ReviseAuthorityLimitHandler> logger,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<AuthorityLimit>(authorityLimitId, cancellationToken);
        if (stream.Aggregate is null)
        {
            logger.LogInformation("Revise authority limit {AuthorityLimitId} failed: record not found", authorityLimitId);
            return TypedResults.NotFound();
        }

        var entity = stream.Aggregate;
        var newLimit = entity.Scope with { MaxLineSize = request.NewMaxGrossPremium, MaxAggregate = request.NewMaxLimit };

        // Clarified 2026-08-09 / FR-010: Revoked is terminal.
        if (entity.Status == "Revoked")
        {
            var rejected = RejectionEvent(entity, newLimit, AuthorityLimitRejectionReasons.RevokedRecord);
            stream.AppendOne(rejected);
            await session.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Authority limit {AuthorityLimitId} revision rejected: {RejectionReason}", authorityLimitId, rejected.RejectionReason);
            return AuthorityRejectionProblem.Conflict(
                authorityLimitId, rejected.RejectionReason, "This authority limit has been revoked and cannot be revised — grant a new one instead.");
        }

        // Clarified 2026-08-09 / FR-012: Underwriter-tier revisions re-run the same
        // cascade check as Grant, against the Cell's current limit.
        if (entity.Tier == "Underwriter")
        {
            var cellGrant = await session.Query<AuthorityLimit>()
                .Where(a => a.Tier == "Cell" && a.CellId == entity.CellId && a.Status == "Active")
                .FirstOrDefaultAsync(cancellationToken);

            var exceedsCellLimit = cellGrant is null || newLimit.MaxLineSize > cellGrant.Scope.MaxLineSize;
            if (exceedsCellLimit)
            {
                var rejected = RejectionEvent(entity, newLimit, AuthorityLimitRejectionReasons.ExceedsCellLimit);
                stream.AppendOne(rejected);
                await session.SaveChangesAsync(cancellationToken);
                logger.LogWarning(
                    "Authority limit {AuthorityLimitId} revision rejected: {RejectionReason}", authorityLimitId, rejected.RejectionReason);
                return AuthorityRejectionProblem.Conflict(
                    authorityLimitId, rejected.RejectionReason, "Revised limit would exceed the cell's own current delegated authority.");
            }
        }

        var revised = new AuthorityLimitRevised(
            authorityLimitId,
            Target: entity.Tier,
            PreviousLimit: entity.Scope,
            NewLimit: newLimit,
            request.Reason,
            RevisedBy: "system", // TODO(ADR-010): populate from the authenticated caller once identity lands
            EffectiveDate: DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date),
            Version: entity.RevisionNumber + 1,
            RevisedAt: DateTimeOffset.UtcNow);

        stream.AppendOne(revised);

        // research.md Decision 4: this module's scope for the "rules changed
        // mid-flight" concern ends at publishing this integration event —
        // 004-underwriting-decisioning consumes it, not this handler.
        await bus.PublishAsync(new AuthorityLimitChangedV1(
            authorityLimitId, ChangeType: "Revised", entity.CellId, entity.UnderwriterId, revised.RevisedAt));

        await session.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Authority limit {AuthorityLimitId} revised to version {Version}", authorityLimitId, revised.Version);

        return TypedResults.Ok(new ReviseAuthorityLimitResponse(authorityLimitId, revised.Version, revised.RevisedAt));
    }

    private static AuthorityLimitRevisionRejected RejectionEvent(AuthorityLimit entity, AuthorityScope attempted, string reason) =>
        new(entity.AuthorityLimitId, attempted, reason, AttemptedBy: "system", RejectedAt: DateTimeOffset.UtcNow);
}
