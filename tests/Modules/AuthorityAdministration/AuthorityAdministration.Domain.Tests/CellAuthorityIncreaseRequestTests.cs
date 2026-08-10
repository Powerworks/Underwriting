using BrokerConnect.Modules.AuthorityAdministration.Domain;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Events;
using Shouldly;

namespace AuthorityAdministration.Domain.Tests;

// [US7]
public class CellAuthorityIncreaseRequestTests
{
    [Fact]
    public void Create_from_CellAuthorityIncreaseRequested_captures_the_request_as_the_auditable_fact()
    {
        var requestId = Guid.NewGuid();
        var current = new AuthorityScope(["Property"], "Bermuda", 30_000_000, 5_000_000);
        var requested = new AuthorityScope(["Property"], "Bermuda", 40_000_000, 7_000_000);

        var @event = new CellAuthorityIncreaseRequested(
            requestId, "CELL-04", "UW-01", "Cell CUO", current, requested,
            "Growing book requires higher line size", DateTimeOffset.UtcNow);

        var entity = CellAuthorityIncreaseRequest.Create(@event);

        entity.RequestId.ShouldBe(requestId);
        entity.CellId.ShouldBe("CELL-04");
        entity.UnderwriterId.ShouldBe("UW-01");
        entity.CurrentLimit.MaxLineSize.ShouldBe(30_000_000);
        entity.RequestedLimit.MaxLineSize.ShouldBe(40_000_000);
    }
}
