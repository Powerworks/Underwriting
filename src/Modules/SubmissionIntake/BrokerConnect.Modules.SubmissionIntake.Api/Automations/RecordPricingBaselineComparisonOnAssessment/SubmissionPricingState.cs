using System.Text.Json.Serialization;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Automations.RecordPricingBaselineComparisonOnAssessment;

/// <summary>
/// Architecture Constraints: "computed live via AggregateStreamAsync&lt;SubmissionPricingState&gt;
/// (never persisted, per Architecture Constraints)". A narrower self-aggregating view of
/// the SAME Submission event stream as the Submission aggregate itself -- only the two
/// facts RecordPricingBaselineComparisonOnAssessmentHandler needs to decide something:
/// the current baseline premium (to compare against), and whether a comparison has
/// already been recorded (idempotency guard). Never registered as a Marten projection/
/// snapshot -- purely a live-aggregation read, recomputed on every handler invocation.
///
/// AggregateStreamAsync&lt;T&gt; walks every event on the Submission stream, so this type
/// defines an Apply overload (no-op, where irrelevant) for every event type that can
/// appear there -- mirrors the Submission aggregate's own exhaustive Create/Apply set,
/// avoiding any dependence on Marten's default behavior for an event type with no
/// matching method.
/// </summary>
public sealed class SubmissionPricingState
{
    public Guid SubmissionId { get; private init; }

    // Marten's document-identity resolution requires the identity member to be
    // literally named Id, even for a type only ever used with AggregateStreamAsync
    // (never persisted/queried as a document) -- see Submission.Id's remarks in the
    // Domain project for the full explanation; same constraint hit here the first
    // time this codebase live-aggregates into a type whose "id" property isn't
    // literally named Id. Private setter (no-op) required too: AggregateStreamAsync
    // compiles a setter delegate for this code path and throws against a get-only
    // property.
    [JsonIgnore]
    public Guid Id
    {
        get => SubmissionId;
        private set { }
    }

    public decimal? BaselinePremium { get; private set; }

    public bool HasComparisonRecorded { get; private set; }

    public static SubmissionPricingState Create(BrokerSubmissionReceived @event) =>
        new() { SubmissionId = @event.SubmissionId };

    public void Apply(BaselinePremiumGenerated @event)
    {
        BaselinePremium = @event.BaselinePremium;
    }

    public void Apply(PricingBaselineAccepted @event)
    {
        HasComparisonRecorded = true;
    }

    public void Apply(PricingBaselineOverridden @event)
    {
        HasComparisonRecorded = true;
    }

    // No-ops: irrelevant to this decision state, but present so every event type on the
    // Submission stream has a matching Apply overload.
    public void Apply(SubmissionNormalized @event)
    {
    }

    public void Apply(SubmissionNormalizationFailed @event)
    {
    }

    public void Apply(SubmissionRoutingRejected @event)
    {
    }

    public void Apply(PotentialDuplicateSubmissionDetected @event)
    {
    }

    public void Apply(SubmissionSuperseded @event)
    {
    }

    public void Apply(SubmissionConfirmedDistinct @event)
    {
    }

    public void Apply(SubmissionManuallyCorrected @event)
    {
    }
}
