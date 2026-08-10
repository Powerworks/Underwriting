using BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.Commands.RequestCellAuthorityIncrease;

public static class RequestCellAuthorityIncreaseHandler
{
    // authorityLimitId here is the Cell's own record — the one
    // UnderwriterAuthorityLimitRejected (US3) was appended onto — used to look up
    // CellId and the current limit; this handler creates a brand-new
    // CellAuthorityIncreaseRequest stream, it does not append to that record.
    [WolverinePost("/api/v1/authority/limits/{authorityLimitId}/increase-requests")]
    public static async Task<Results<Created<RequestCellAuthorityIncreaseResponse>, NotFound>> Handle(
        Guid authorityLimitId,
        RequestCellAuthorityIncreaseRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var cellGrant = await session.Events.AggregateStreamAsync<AuthorityLimit>(authorityLimitId, token: cancellationToken);
        if (cellGrant is null)
            return TypedResults.NotFound();

        var requestId = Guid.NewGuid();
        var requested = new CellAuthorityIncreaseRequested(
            requestId,
            cellGrant.CellId,
            request.UnderwriterId,
            RequestedBy: "system", // TODO(ADR-010): populate from the authenticated caller once identity lands
            CurrentLimit: cellGrant.Scope,
            request.RequestedLimit,
            request.Justification,
            RequestedAt: DateTimeOffset.UtcNow);

        session.Events.StartStream<CellAuthorityIncreaseRequest>(requestId, requested);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v1/authority/increase-requests/{requestId}",
            new RequestCellAuthorityIncreaseResponse(requestId, cellGrant.CellId, request.UnderwriterId, requested.RequestedAt));
    }
}
