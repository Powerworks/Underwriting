using System.Text.Json.Serialization;

namespace BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;

/// <summary>
/// Separate, unrelated single-event stream (audit-only reference data), started by
/// `PricingModelVersionDeployed`. See specs/002-submission-intake/design.md
/// Components — `PricingModel` aggregate and Unresolved Questions (stream identity):
/// board gives no `id`-flagged field on the event (`modelVersion` is not marked
/// `id`), so identity is a generated `Guid` rather than keying on `modelVersion`.
/// `Create`/`Apply(PricingModelVersionDeployed)` deferred to Phase 9.
/// </summary>
public sealed class PricingModel
{
    [JsonInclude]
    public Guid Id { get; private init; }

    [JsonInclude]
    public string ModelVersion { get; private init; } = string.Empty;

    [JsonInclude]
    public string DeployedBy { get; private init; } = string.Empty;

    [JsonInclude]
    public DateTimeOffset DeployedAt { get; private init; }

    [JsonInclude]
    public string? ChangeSummary { get; private init; }

    [JsonConstructor]
    private PricingModel(
        Guid id, string modelVersion, string deployedBy,
        DateTimeOffset deployedAt, string? changeSummary)
    {
        Id = id;
        ModelVersion = modelVersion;
        DeployedBy = deployedBy;
        DeployedAt = deployedAt;
        ChangeSummary = changeSummary;
    }
}
