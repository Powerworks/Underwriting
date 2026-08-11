using BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionQueue;
using Shouldly;
using Xunit;
// Namespace and document type share the name SubmissionQueue (matches
// SubmissionQueueProjector.cs's own qualification style) -- alias needed to
// reference the type unambiguously.
using SubmissionQueueDoc = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionQueue.SubmissionQueue;

namespace SubmissionIntake.Api.Tests;

// T4.8: Layer 2 test per design.md Test Strategy — "Query handlers
// (GetSubmissionQueue, etc.): filter/pagination logic, in-memory list."
// Marten's Query<T>() LINQ provider needs a real store, so the handler
// splits the DB round trip from the pure filter/pagination logic
// (GetSubmissionQueue.FilterAndPaginate), which is exercised directly here
// against an in-memory list — no Marten, no NSubstitute session needed.
public class GetSubmissionQueueTests
{
    private static SubmissionQueueDoc MakeItem(
        string brokerFirmId = "acme-brokers",
        string namedInsured = "Acme Corp",
        string classOfBusiness = "Property",
        bool isPossibleDuplicate = false) => new()
    {
        SubmissionId = Guid.NewGuid(),
        BrokerFirmId = brokerFirmId,
        NamedInsured = namedInsured,
        ClassOfBusiness = classOfBusiness,
        Territory = "TX",
        LineSizeSought = 1_000_000m,
        ReceivedAt = DateTimeOffset.UtcNow,
        Status = "Ready",
        IsPossibleDuplicate = isPossibleDuplicate,
    };

    [Fact]
    public void Filters_by_brokerFirmId()
    {
        var items = new[]
        {
            MakeItem(brokerFirmId: "acme-brokers"),
            MakeItem(brokerFirmId: "other-brokers"),
        };

        var response = GetSubmissionQueue.FilterAndPaginate(
            items, brokerFirmId: "acme-brokers", classOfBusiness: null, search: null, page: null, pageSize: null);

        response.Items.Count.ShouldBe(1);
        response.Items[0].BrokerFirmId.ShouldBe("acme-brokers");
        response.TotalCount.ShouldBe(1);
    }

    [Fact]
    public void Filters_by_classOfBusiness()
    {
        var items = new[]
        {
            MakeItem(classOfBusiness: "Property"),
            MakeItem(classOfBusiness: "Casualty"),
        };

        var response = GetSubmissionQueue.FilterAndPaginate(
            items, brokerFirmId: null, classOfBusiness: "Casualty", search: null, page: null, pageSize: null);

        response.Items.Count.ShouldBe(1);
        response.Items[0].ClassOfBusiness.ShouldBe("Casualty");
    }

    [Fact]
    public void Search_matches_namedInsured_or_brokerFirmId_case_insensitively()
    {
        var items = new[]
        {
            MakeItem(brokerFirmId: "acme-brokers", namedInsured: "Widget Co"),
            MakeItem(brokerFirmId: "zenith-brokers", namedInsured: "Gadget Inc"),
        };

        var response = GetSubmissionQueue.FilterAndPaginate(
            items, brokerFirmId: null, classOfBusiness: null, search: "WIDGET", page: null, pageSize: null);

        response.Items.Count.ShouldBe(1);
        response.Items[0].NamedInsured.ShouldBe("Widget Co");

        var byBroker = GetSubmissionQueue.FilterAndPaginate(
            items, brokerFirmId: null, classOfBusiness: null, search: "ZENITH", page: null, pageSize: null);

        byBroker.Items.Count.ShouldBe(1);
        byBroker.Items[0].BrokerFirmId.ShouldBe("zenith-brokers");
    }

    [Fact]
    public void Defaults_to_page_1_pageSize_20()
    {
        var items = Enumerable.Range(0, 25).Select(_ => MakeItem()).ToList();

        var response = GetSubmissionQueue.FilterAndPaginate(
            items, brokerFirmId: null, classOfBusiness: null, search: null, page: null, pageSize: null);

        response.Page.ShouldBe(1);
        response.PageSize.ShouldBe(20);
        response.Items.Count.ShouldBe(20);
        response.TotalCount.ShouldBe(25);
    }

    [Fact]
    public void Second_page_returns_remaining_items()
    {
        var items = Enumerable.Range(0, 25).Select(_ => MakeItem()).ToList();

        var response = GetSubmissionQueue.FilterAndPaginate(
            items, brokerFirmId: null, classOfBusiness: null, search: null, page: 2, pageSize: 20);

        response.Page.ShouldBe(2);
        response.Items.Count.ShouldBe(5);
        response.TotalCount.ShouldBe(25);
    }

    [Fact]
    public void PageSize_is_capped_at_200()
    {
        var items = Enumerable.Range(0, 5).Select(_ => MakeItem()).ToList();

        var response = GetSubmissionQueue.FilterAndPaginate(
            items, brokerFirmId: null, classOfBusiness: null, search: null, page: 1, pageSize: 500);

        response.PageSize.ShouldBe(200);
    }

    [Fact]
    public void PossibleDuplicateCount_counts_only_flagged_items_in_the_filtered_set()
    {
        var items = new[]
        {
            MakeItem(isPossibleDuplicate: true),
            MakeItem(isPossibleDuplicate: true),
            MakeItem(isPossibleDuplicate: false),
        };

        var response = GetSubmissionQueue.FilterAndPaginate(
            items, brokerFirmId: null, classOfBusiness: null, search: null, page: null, pageSize: null);

        response.PossibleDuplicateCount.ShouldBe(2);
        response.TotalCount.ShouldBe(3);
    }
}
