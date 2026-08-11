using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionQueue;

public static class GetSubmissionQueue
{
    // design.md Performance Considerations: "paginated from v1 ... consistent
    // with 001's GetCellAuthorityRegister pattern" -- default/max mirrored
    // from that precedent (itself flagged there as a guess, not a
    // transcribed value).
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    [WolverineGet("/api/v1/submission-intake/submission-queue")]
    public static async Task<Ok<SubmissionQueueResponse>> Handle(
        string? brokerFirmId,
        string? classOfBusiness,
        string? search,
        int? page,
        int? pageSize,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        // Marten's LINQ provider needs a real store, so the DB round trip
        // (broad load) and the filter/pagination logic (pure, unit-testable
        // per design.md Test Strategy's Layer 2 "filter/pagination logic,
        // in-memory") are kept separate.
        var allItems = await session.Query<SubmissionQueue>().ToListAsync(cancellationToken);

        var response = FilterAndPaginate(allItems, brokerFirmId, classOfBusiness, search, page, pageSize);

        return TypedResults.Ok(response);
    }

    public static SubmissionQueueResponse FilterAndPaginate(
        IReadOnlyList<SubmissionQueue> items,
        string? brokerFirmId,
        string? classOfBusiness,
        string? search,
        int? page,
        int? pageSize)
    {
        var filtered = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(brokerFirmId))
        {
            filtered = filtered.Where(i => i.BrokerFirmId == brokerFirmId);
        }

        if (!string.IsNullOrWhiteSpace(classOfBusiness))
        {
            filtered = filtered.Where(i => i.ClassOfBusiness == classOfBusiness);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Screens: "Search box | new query param search (matches
            // namedInsured/brokerFirmId, case-insensitive)".
            filtered = filtered.Where(i =>
                i.NamedInsured.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.BrokerFirmId.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var filteredList = filtered.ToList();

        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        var pagedItems = filteredList
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(i => new SubmissionQueueItem(
                i.SubmissionId, i.BrokerFirmId, i.NamedInsured, i.ClassOfBusiness,
                i.Territory, i.LineSizeSought, i.ReceivedAt, i.Status,
                i.IsPossibleDuplicate, i.SuspectedOriginalSubmissionId))
            .ToList();

        return new SubmissionQueueResponse(
            pagedItems,
            effectivePage,
            effectivePageSize,
            TotalCount: filteredList.Count,
            PossibleDuplicateCount: filteredList.Count(i => i.IsPossibleDuplicate));
    }
}

public sealed record SubmissionQueueResponse(
    IReadOnlyList<SubmissionQueueItem> Items,
    int Page, int PageSize, int TotalCount, int PossibleDuplicateCount);

public sealed record SubmissionQueueItem(
    Guid SubmissionId, string BrokerFirmId, string NamedInsured, string ClassOfBusiness,
    string Territory, decimal LineSizeSought, DateTimeOffset ReceivedAt, string Status,
    bool IsPossibleDuplicate, Guid? SuspectedOriginalSubmissionId);
