using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionExceptionQueue;

public static class GetSubmissionExceptionQueue
{
    // design.md Performance Considerations: "SubmissionQueue/SubmissionExceptionQueue/
    // BrokerAuthorizationExceptionLog list endpoints are paginated from v1
    // (constitution Principle VII), consistent with 001's GetCellAuthorityRegister
    // pattern" -- default/max mirrored from 4.8's GetSubmissionQueue.
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    // Route inferred, consistent with 4.8's GetSubmissionQueue convention -- not
    // board-specified (design.md has no Screens entry for this read model, unlike
    // SubmissionQueue which transcribes a real board screen).
    [WolverineGet("/api/v1/submission-intake/submission-exception-queue")]
    public static async Task<Ok<SubmissionExceptionQueueResponse>> Handle(
        string? brokerFirmId,
        string? status,
        int? page,
        int? pageSize,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        // Same split as 4.8's GetSubmissionQueue: Marten's LINQ provider needs a
        // real store (broad load), filter/pagination logic stays pure and
        // unit-testable per design.md Test Strategy's Layer 2 line.
        var allItems = await session.Query<SubmissionExceptionQueue>().ToListAsync(cancellationToken);

        var response = FilterAndPaginate(allItems, brokerFirmId, status, page, pageSize);

        return TypedResults.Ok(response);
    }

    public static SubmissionExceptionQueueResponse FilterAndPaginate(
        IReadOnlyList<SubmissionExceptionQueue> items,
        string? brokerFirmId,
        string? status,
        int? page,
        int? pageSize)
    {
        var filtered = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(brokerFirmId))
        {
            filtered = filtered.Where(i => i.BrokerFirmId == brokerFirmId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(i => i.Status == status);
        }

        var filteredList = filtered.ToList();

        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        var pagedItems = filteredList
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(i => new SubmissionExceptionQueueItem(
                i.SubmissionId, i.BrokerFirmId, i.FailureReason, i.AttemptedAt, i.Status))
            .ToList();

        return new SubmissionExceptionQueueResponse(
            pagedItems,
            effectivePage,
            effectivePageSize,
            TotalCount: filteredList.Count);
    }
}

public sealed record SubmissionExceptionQueueResponse(
    IReadOnlyList<SubmissionExceptionQueueItem> Items,
    int Page, int PageSize, int TotalCount);

public sealed record SubmissionExceptionQueueItem(
    Guid SubmissionId, string BrokerFirmId, string FailureReason, DateTimeOffset AttemptedAt, string Status);
