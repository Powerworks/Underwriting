using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Shouldly;

namespace SubmissionIntake.Domain.Tests;

// 9.1 — [FR-12, AC-12.1]. design.md Unresolved Questions: PricingModelVersionDeployed
// has no id-flagged field (modelVersion isn't marked id) -- stream identity is a
// generated Guid, decided by the caller, not derived from the event (Foundational's
// 2.4 decision). PricingModel.Create takes that id explicitly rather than relying on
// Marten's AggregateStreamAsync/IEvent-injection conventions, which this aggregate
// never actually exercises (no automation/query ever re-aggregates a PricingModel
// stream -- design.md: "no read model", audit-only reference data).
public class PricingModelTests
{
    [Fact]
    public void Create_from_PricingModelVersionDeployed_sets_all_fields()
    {
        var id = Guid.NewGuid();
        var deployedAt = DateTimeOffset.UtcNow;
        var deployed = new PricingModelVersionDeployed(
            "rating-model-v3", "actuarial-jane", deployedAt, "Recalibrated wind exposure segmentation");

        var entity = PricingModel.Create(id, deployed);

        entity.Id.ShouldBe(id);
        entity.ModelVersion.ShouldBe("rating-model-v3");
        entity.DeployedBy.ShouldBe("actuarial-jane");
        entity.DeployedAt.ShouldBe(deployedAt);
        entity.ChangeSummary.ShouldBe("Recalibrated wind exposure segmentation");
    }
}
