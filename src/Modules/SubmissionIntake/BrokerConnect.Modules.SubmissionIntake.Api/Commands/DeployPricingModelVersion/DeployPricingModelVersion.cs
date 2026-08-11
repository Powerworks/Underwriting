using System.ComponentModel.DataAnnotations;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Commands.DeployPricingModelVersion;

public sealed record DeployPricingModelVersionRequest(
    [property: Required] string ModelVersion,
    [property: Required] string DeployedBy,
    string? ChangeSummary);

public sealed record DeployPricingModelVersionResponse(
    Guid PricingModelId, string ModelVersion, DateTimeOffset DeployedAt);
