using BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.BrokerAuthorizationExceptionLog;
using Shouldly;
using Xunit;
// Namespace and document type share the name BrokerAuthorizationExceptionLog (matches
// BrokerAuthorizationExceptionLogProjector.cs's own qualification style) -- alias
// needed to reference the type unambiguously.
using BrokerAuthorizationExceptionLogDoc = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.BrokerAuthorizationExceptionLog.BrokerAuthorizationExceptionLog;

namespace SubmissionIntake.Api.Tests;

// T6.7: Layer 2 test per design.md Test Strategy — "Query handlers (GetSubmissionQueue,
// etc.): filter/pagination logic, in-memory list." Mirrors 4.8/4.10's precedent: Marten's
// Query<T>() LINQ provider needs a real store, so the handler splits the DB round trip
// from the pure filter/pagination logic (GetBrokerAuthorizationExceptionLog.FilterAndPaginate),
// exercised directly here against an in-memory list -- no Marten, no NSubstitute session
// needed.
public class GetBrokerAuthorizationExceptionLogTests
{
    private static BrokerAuthorizationExceptionLogDoc MakeItem(
        string brokerFirmId = "acme-brokers",
        string requestedCellId = "cell-01",
        string? requestedClassOfBusiness = "Property",
        string rejectionReason = "PanelDoesNotCoverCell",
        string reviewStatus = "Pending") => new()
    {
        SubmissionId = Guid.NewGuid(),
        BrokerFirmId = brokerFirmId,
        RequestedCellId = requestedCellId,
        RequestedClassOfBusiness = requestedClassOfBusiness,
        RejectionReason = rejectionReason,
        RejectedAt = DateTimeOffset.UtcNow,
        ReviewStatus = reviewStatus,
    };

    [Fact]
    public void Filters_by_brokerFirmId()
    {
        var items = new[]
        {
            MakeItem(brokerFirmId: "acme-brokers"),
            MakeItem(brokerFirmId: "other-brokers"),
        };

        var response = GetBrokerAuthorizationExceptionLog.FilterAndPaginate(
            items, brokerFirmId: "acme-brokers", page: null, pageSize: null);

        response.Items.Count.ShouldBe(1);
        response.Items[0].BrokerFirmId.ShouldBe("acme-brokers");
        response.TotalCount.ShouldBe(1);
    }

    [Fact]
    public void Defaults_to_page_1_pageSize_20()
    {
        var items = Enumerable.Range(0, 25).Select(_ => MakeItem()).ToList();

        var response = GetBrokerAuthorizationExceptionLog.FilterAndPaginate(
            items, brokerFirmId: null, page: null, pageSize: null);

        response.Page.ShouldBe(1);
        response.PageSize.ShouldBe(20);
        response.Items.Count.ShouldBe(20);
        response.TotalCount.ShouldBe(25);
    }

    [Fact]
    public void Second_page_returns_remaining_items()
    {
        var items = Enumerable.Range(0, 25).Select(_ => MakeItem()).ToList();

        var response = GetBrokerAuthorizationExceptionLog.FilterAndPaginate(
            items, brokerFirmId: null, page: 2, pageSize: 20);

        response.Page.ShouldBe(2);
        response.Items.Count.ShouldBe(5);
        response.TotalCount.ShouldBe(25);
    }

    [Fact]
    public void PageSize_is_capped_at_200()
    {
        var items = Enumerable.Range(0, 5).Select(_ => MakeItem()).ToList();

        var response = GetBrokerAuthorizationExceptionLog.FilterAndPaginate(
            items, brokerFirmId: null, page: 1, pageSize: 500);

        response.PageSize.ShouldBe(200);
    }

    [Fact]
    public void Maps_all_dto_fields_from_the_document()
    {
        var item = MakeItem(
            brokerFirmId: "acme-brokers", requestedCellId: "cell-01",
            requestedClassOfBusiness: "Property", rejectionReason: "PanelDoesNotCoverCell",
            reviewStatus: "Pending");
        var items = new[] { item };

        var response = GetBrokerAuthorizationExceptionLog.FilterAndPaginate(
            items, brokerFirmId: null, page: null, pageSize: null);

        var dto = response.Items[0];
        dto.SubmissionId.ShouldBe(item.SubmissionId);
        dto.BrokerFirmId.ShouldBe("acme-brokers");
        dto.RequestedCellId.ShouldBe("cell-01");
        dto.RequestedClassOfBusiness.ShouldBe("Property");
        dto.RejectionReason.ShouldBe("PanelDoesNotCoverCell");
        dto.RejectedAt.ShouldBe(item.RejectedAt);
        dto.ReviewStatus.ShouldBe("Pending");
    }
}
