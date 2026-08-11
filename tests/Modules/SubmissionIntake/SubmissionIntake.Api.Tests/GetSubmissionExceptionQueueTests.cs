using BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionExceptionQueue;
using Shouldly;
using Xunit;
// Namespace and document type share the name SubmissionExceptionQueue (matches
// SubmissionExceptionQueueProjector.cs's own qualification style) -- alias
// needed to reference the type unambiguously.
using SubmissionExceptionQueueDoc = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionExceptionQueue.SubmissionExceptionQueue;

namespace SubmissionIntake.Api.Tests;

// T4.10: Layer 2 test per design.md Test Strategy — "Query handlers
// (GetSubmissionQueue, etc.): filter/pagination logic, in-memory list."
// Mirrors 4.8's GetSubmissionQueueTests: Marten's Query<T>() LINQ provider
// needs a real store, so the handler splits the DB round trip from the pure
// filter/pagination logic (GetSubmissionExceptionQueue.FilterAndPaginate),
// exercised directly here against an in-memory list -- no Marten, no
// NSubstitute session needed.
public class GetSubmissionExceptionQueueTests
{
    private static SubmissionExceptionQueueDoc MakeItem(
        string brokerFirmId = "acme-brokers",
        string failureReason = "MissingClassCode",
        string status = "Failed") => new()
    {
        SubmissionId = Guid.NewGuid(),
        BrokerFirmId = brokerFirmId,
        FailureReason = failureReason,
        AttemptedAt = DateTimeOffset.UtcNow,
        Status = status,
    };

    [Fact]
    public void Filters_by_brokerFirmId()
    {
        var items = new[]
        {
            MakeItem(brokerFirmId: "acme-brokers"),
            MakeItem(brokerFirmId: "other-brokers"),
        };

        var response = GetSubmissionExceptionQueue.FilterAndPaginate(
            items, brokerFirmId: "acme-brokers", status: null, page: null, pageSize: null);

        response.Items.Count.ShouldBe(1);
        response.Items[0].BrokerFirmId.ShouldBe("acme-brokers");
        response.TotalCount.ShouldBe(1);
    }

    [Fact]
    public void Filters_by_status()
    {
        var items = new[]
        {
            MakeItem(status: "Failed"),
            MakeItem(status: "Corrected"),
        };

        var response = GetSubmissionExceptionQueue.FilterAndPaginate(
            items, brokerFirmId: null, status: "Corrected", page: null, pageSize: null);

        response.Items.Count.ShouldBe(1);
        response.Items[0].Status.ShouldBe("Corrected");
    }

    [Fact]
    public void Defaults_to_page_1_pageSize_20()
    {
        var items = Enumerable.Range(0, 25).Select(_ => MakeItem()).ToList();

        var response = GetSubmissionExceptionQueue.FilterAndPaginate(
            items, brokerFirmId: null, status: null, page: null, pageSize: null);

        response.Page.ShouldBe(1);
        response.PageSize.ShouldBe(20);
        response.Items.Count.ShouldBe(20);
        response.TotalCount.ShouldBe(25);
    }

    [Fact]
    public void Second_page_returns_remaining_items()
    {
        var items = Enumerable.Range(0, 25).Select(_ => MakeItem()).ToList();

        var response = GetSubmissionExceptionQueue.FilterAndPaginate(
            items, brokerFirmId: null, status: null, page: 2, pageSize: 20);

        response.Page.ShouldBe(2);
        response.Items.Count.ShouldBe(5);
        response.TotalCount.ShouldBe(25);
    }

    [Fact]
    public void PageSize_is_capped_at_200()
    {
        var items = Enumerable.Range(0, 5).Select(_ => MakeItem()).ToList();

        var response = GetSubmissionExceptionQueue.FilterAndPaginate(
            items, brokerFirmId: null, status: null, page: 1, pageSize: 500);

        response.PageSize.ShouldBe(200);
    }

    [Fact]
    public void Maps_all_dto_fields_from_the_document()
    {
        var item = MakeItem(brokerFirmId: "acme-brokers", failureReason: "MissingClassCode", status: "Failed");
        var items = new[] { item };

        var response = GetSubmissionExceptionQueue.FilterAndPaginate(
            items, brokerFirmId: null, status: null, page: null, pageSize: null);

        var dto = response.Items[0];
        dto.SubmissionId.ShouldBe(item.SubmissionId);
        dto.BrokerFirmId.ShouldBe("acme-brokers");
        dto.FailureReason.ShouldBe("MissingClassCode");
        dto.AttemptedAt.ShouldBe(item.AttemptedAt);
        dto.Status.ShouldBe("Failed");
    }
}
