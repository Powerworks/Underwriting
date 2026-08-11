using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.BrokerAuthorizationExceptionLog;

public static class GetBrokerAuthorizationExceptionLog
{
    // design.md Performance Considerations: "SubmissionQueue/SubmissionExceptionQueue/
    // BrokerAuthorizationExceptionLog list endpoints are paginated from v1
    // (constitution Principle VII), consistent with 001's GetCellAuthorityRegister
    // pattern" -- default/max mirrored from 4.8/4.10's precedent.
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    // Route inferred, consistent with 4.8/4.10 convention -- requirements.md's US-2
    // narrative says this log is "never appears in any underwriter's queue" and is
    // visible to ops/broker-relationship-management instead; that's an authorization
    // concern handled at a higher layer (not modeled in this spec), so the endpoint
    // itself is shaped identically to the other two list endpoints.
    [WolverineGet("/api/v1/submission-intake/broker-authorization-exception-log")]
    public static async Task<Ok<BrokerAuthorizationExceptionLogResponse>> Handle(
        string? brokerFirmId,
        int? page,
        int? pageSize,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        // Same split as 4.8/4.10: Marten's LINQ provider needs a real store (broad
        // load), filter/pagination logic stays pure and unit-testable per design.md
        // Test Strategy's Layer 2 line.
        var allItems = await session.Query<BrokerAuthorizationExceptionLog>().ToListAsync(cancellationToken);

        var response = FilterAndPaginate(allItems, brokerFirmId, page, pageSize);

        return TypedResults.Ok(response);
    }

    public static BrokerAuthorizationExceptionLogResponse FilterAndPaginate(
        IReadOnlyList<BrokerAuthorizationExceptionLog> items,
        string? brokerFirmId,
        int? page,
        int? pageSize)
    {
        var filtered = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(brokerFirmId))
        {
            filtered = filtered.Where(i => i.BrokerFirmId == brokerFirmId);
        }

        var filteredList = filtered.ToList();

        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        var pagedItems = filteredList
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(i => new BrokerAuthorizationExceptionLogItem(
                i.SubmissionId, i.BrokerFirmId, i.RequestedCellId, i.RequestedClassOfBusiness,
                i.RejectionReason, i.RejectedAt, i.ReviewStatus))
            .ToList();

        return new BrokerAuthorizationExceptionLogResponse(
            pagedItems,
            effectivePage,
            effectivePageSize,
            TotalCount: filteredList.Count);
    }
}

public sealed record BrokerAuthorizationExceptionLogResponse(
    IReadOnlyList<BrokerAuthorizationExceptionLogItem> Items,
    int Page, int PageSize, int TotalCount);

public sealed record BrokerAuthorizationExceptionLogItem(
    Guid SubmissionId, string BrokerFirmId, string RequestedCellId, string? RequestedClassOfBusiness,
    string RejectionReason, DateTimeOffset RejectedAt, string ReviewStatus);
