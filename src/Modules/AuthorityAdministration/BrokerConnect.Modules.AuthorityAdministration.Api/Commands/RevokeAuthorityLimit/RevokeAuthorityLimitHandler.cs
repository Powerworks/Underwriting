using BrokerConnect.Modules.AuthorityAdministration.Api.IntegrationEvents.Published;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine;
using Wolverine.Http;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.Commands.RevokeAuthorityLimit;

public static class RevokeAuthorityLimitHandler
{
    [WolverinePost("/api/v1/authority/limits/{authorityLimitId}/revocation")]
    public static async Task<Results<Ok<RevokeAuthorityLimitResponse>, NotFound>> Handle(
        Guid authorityLimitId,
        RevokeAuthorityLimitRequest request,
        IDocumentSession session,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<AuthorityLimit>(authorityLimitId, cancellationToken);
        if (stream.Aggregate is null)
            return TypedResults.NotFound();

        var entity = stream.Aggregate;

        var revoked = new AuthorityLimitRevoked(
            authorityLimitId,
            Target: entity.Tier,
            entity.CellId,
            PriorLimit: entity.Scope,
            request.Reason,
            RevokedBy: "system", // TODO(ADR-010): populate from the authenticated caller once identity lands
            request.Immediacy,
            RevokedAt: DateTimeOffset.UtcNow);

        stream.AppendOne(revoked);

        // research.md Decision 4 — same publish as ReviseAuthorityLimitHandler;
        // revocation is the higher-urgency half of the "rules changed mid-flight" trigger.
        await bus.PublishAsync(new AuthorityLimitChangedV1(
            authorityLimitId, ChangeType: "Revoked", entity.CellId, entity.UnderwriterId, revoked.RevokedAt));

        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RevokeAuthorityLimitResponse(authorityLimitId, revoked.RevokedAt));
    }
}
