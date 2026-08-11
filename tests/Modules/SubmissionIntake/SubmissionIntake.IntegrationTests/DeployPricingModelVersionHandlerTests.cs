using BrokerConnect.Modules.SubmissionIntake.Api.Commands.DeployPricingModelVersion;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace SubmissionIntake.IntegrationTests;

// 9.3 -- Layer 3 (Testcontainers-backed Postgres, real StartStream/FetchStreamAsync),
// same shared fixture as 4.4/5.7/8.3 (SubmissionIntakePostgresFixture). Per design.md
// Commands table: DeployPricingModelVersion -> POST .../pricing-models ->
// PricingModelVersionDeployed (StartStream<PricingModel>), no read model. Unresolved
// Questions (resolved): stream identity is a generated Guid, not derived from the event.
//
// RED: DeployPricingModelVersionHandler does not exist yet (created in 9.4). This file
// is expected to fail to compile until then -- that is the correct RED state.
[Collection(SubmissionIntakePostgresCollection.Name)]
public class DeployPricingModelVersionHandlerTests(SubmissionIntakePostgresFixture fixture)
{
    [Fact]
    public async Task DeployPricingModelVersion_starts_a_new_PricingModel_stream_with_a_fresh_generated_id()
    {
        await using var session = fixture.Store.LightweightSession();
        var request = new DeployPricingModelVersionRequest(
            "rating-model-v4", "actuarial-jane", "Recalibrated coastal wind exposure segmentation");

        var response = await DeployPricingModelVersionHandler.Handle(
            request, session, NullLogger<DeployPricingModelVersionHandler>.Instance, CancellationToken.None);

        response.ShouldNotBeNull();
        response.PricingModelId.ShouldNotBe(Guid.Empty);

        await using var querySession = fixture.Store.LightweightSession();
        var events = await querySession.Events.FetchStreamAsync(response.PricingModelId);
        events.Count.ShouldBe(1);
        var deployed = events.Select(e => e.Data).OfType<PricingModelVersionDeployed>().Single();
        deployed.ModelVersion.ShouldBe("rating-model-v4");
        deployed.DeployedBy.ShouldBe("actuarial-jane");
        deployed.ChangeSummary.ShouldBe("Recalibrated coastal wind exposure segmentation");
    }

    // A second deployment starts an entirely separate, unrelated stream (design.md:
    // "PricingModel is a separate, unrelated stream" per deployment -- no shared
    // identity, no update-in-place).
    [Fact]
    public async Task Each_deployment_starts_its_own_independent_stream()
    {
        await using var session = fixture.Store.LightweightSession();
        var first = await DeployPricingModelVersionHandler.Handle(
            new DeployPricingModelVersionRequest("rating-model-v4", "actuarial-jane", null),
            session, NullLogger<DeployPricingModelVersionHandler>.Instance, CancellationToken.None);
        var second = await DeployPricingModelVersionHandler.Handle(
            new DeployPricingModelVersionRequest("rating-model-v5", "actuarial-jane", null),
            session, NullLogger<DeployPricingModelVersionHandler>.Instance, CancellationToken.None);

        first.PricingModelId.ShouldNotBe(second.PricingModelId);

        await using var querySession = fixture.Store.LightweightSession();
        var firstEvents = await querySession.Events.FetchStreamAsync(first.PricingModelId);
        var secondEvents = await querySession.Events.FetchStreamAsync(second.PricingModelId);
        firstEvents.Count.ShouldBe(1);
        secondEvents.Count.ShouldBe(1);
    }
}
