using System.Text.Json.Serialization;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;

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

    // design.md Unresolved Questions: stream identity is a generated Guid decided by the
    // caller (Foundational's 2.4 decision) -- id is an explicit parameter here rather than
    // relying on Marten's AggregateStreamAsync/IEvent-injection conventions, which this
    // aggregate never actually exercises (no automation/query ever re-aggregates a
    // PricingModel stream -- design.md: "no read model", audit-only reference data). The
    // handler (9.4) generates the same Guid it passes to both Create and StartStream.
    public static PricingModel Create(Guid id, PricingModelVersionDeployed @event) => new(
        id, @event.ModelVersion, @event.DeployedBy, @event.DeployedAt, @event.ChangeSummary);

    // No-op in practice: PricingModel is a single-event, stream-starting aggregate --
    // PricingModelVersionDeployed is only ever the FIRST event on its stream (Create's
    // job above), so nothing in this spec ever calls Apply for it. Present for structural
    // symmetry with Submission's Create/Apply split.
    public void Apply(PricingModelVersionDeployed @event)
    {
    }
}
