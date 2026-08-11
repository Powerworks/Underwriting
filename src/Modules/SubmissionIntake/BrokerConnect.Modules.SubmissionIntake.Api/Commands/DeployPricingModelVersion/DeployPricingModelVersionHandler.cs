using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;
using Microsoft.Extensions.Logging;
using Wolverine.Http;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Commands.DeployPricingModelVersion;

public class DeployPricingModelVersionHandler
{
    // TODO(ADR-010): [Authorize] policy deferred until the identity-provider decision
    // (Solution Arch §8, DEC-032) lands -- actuarial/admin role-gated per design.md
    // Security Considerations, same TODO pattern as SupersedeSubmissionHandler (5.8)/
    // CorrectSubmissionHandler (8.4).
    //
    // design.md Commands table: PricingModel is "a separate, unrelated stream" from
    // Submission -- every deployment StartStreams a brand-new PricingModel with a
    // freshly generated Guid (Unresolved Questions, resolved: no id-flagged field on
    // the event to key on instead). No read model consumes this per design.md's own
    // note ("audit reference data only"), so this handler has no NotFound path -- it
    // always creates.
    [WolverinePost("/api/v1/submission-intake/pricing-models")]
    public static async Task<DeployPricingModelVersionResponse> Handle(
        DeployPricingModelVersionRequest request,
        IDocumentSession session,
        ILogger<DeployPricingModelVersionHandler> logger,
        CancellationToken cancellationToken)
    {
        var pricingModelId = Guid.NewGuid();
        var deployed = new PricingModelVersionDeployed(
            request.ModelVersion, request.DeployedBy, DeployedAt: DateTimeOffset.UtcNow, request.ChangeSummary);

        session.Events.StartStream<PricingModel>(pricingModelId, deployed);
        await session.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Pricing model version {ModelVersion} deployed by {DeployedBy} as {PricingModelId}",
            request.ModelVersion, request.DeployedBy, pricingModelId);

        return new DeployPricingModelVersionResponse(pricingModelId, request.ModelVersion, deployed.DeployedAt);
    }
}
