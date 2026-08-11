namespace BrokerConnect.Modules.SubmissionIntake.Api.IntegrationEvents.Consumers;

/// <summary>
/// ASSUMED integration event from Underwriting Decisioning (context 2, not yet built)
/// -- design.md Unresolved Questions: "assumed integration event ... carrying at
/// minimum submissionId, proposedPremium/proposedTerms, underwriterId. Confirm against
/// that context's own design once it exists -- mirrors 001's AuthorityLimitChangedV1
/// cross-module flag, but in the consuming direction." Published when Underwriting
/// Decisioning's AssessSubmission command (S2.1) completes; consumed here by
/// RecordPricingBaselineComparisonOnAssessment (ADR-004: integration event, never a
/// direct cross-module call). Field shape is the stubbed interim contract per
/// design.md's Implementation Step 12 -- confirm/adjust once Underwriting Decisioning's
/// own design.md exists.
/// </summary>
public sealed record SubmissionAssessedV1(
    Guid SubmissionId,
    decimal ProposedPremium,
    string UnderwriterId,
    DateTimeOffset AssessedAt);
