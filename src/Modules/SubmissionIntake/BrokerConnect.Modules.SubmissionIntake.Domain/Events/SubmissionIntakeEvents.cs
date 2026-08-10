namespace BrokerConnect.Modules.SubmissionIntake.Domain.Events;

/// <summary>US-1/FR-1 — StartStream. Always succeeds if the payload arrives; risk detail moves to <see cref="SubmissionNormalized"/> once extraction succeeds.</summary>
public sealed record BrokerSubmissionReceived(
    Guid SubmissionId,
    string BrokerFirmId,
    string SubmittingContact,
    string RawPayloadRef,
    string SourceChannel,
    DateTimeOffset ReceivedAt);

/// <summary>US-2/FR-2 — broker's panel authorization doesn't cover the requested cell/class. Never silently dropped.</summary>
public sealed record SubmissionRoutingRejected(
    Guid SubmissionId,
    string BrokerFirmId,
    string RequestedCellId,
    string? RequestedClassOfBusiness,
    string RejectionReason,
    DateTimeOffset RejectedAt);

/// <summary>US-3/FR-3 — ADEPT normalization succeeded; structured fields extracted from the raw payload.</summary>
public sealed record SubmissionNormalized(
    Guid SubmissionId,
    string ClassOfBusiness,
    string Territory,
    decimal LineSizeSought,
    string? KeyTerms,
    string NamedInsured,
    DateOnly EffectiveDateRequested,
    string NormalizationStatus,
    DateTimeOffset NormalizedAt);

/// <summary>US-4/FR-4 — missing required field, malformed data, unrecognized class code, or schema validation error. Raw payload always preserved.</summary>
public sealed record SubmissionNormalizationFailed(
    Guid SubmissionId,
    string FailureReason,
    string RawPayloadRef,
    DateTimeOffset AttemptedAt);

/// <summary>US-5/FR-5 — manual remediation path: operations or the broker (re-sending) fixes the data, re-triggering normalization.</summary>
public sealed record SubmissionManuallyCorrected(
    Guid SubmissionId,
    string CorrectedBy,
    string CorrectionDescription,
    bool ResubmittedForNormalization,
    DateTimeOffset CorrectedAt);

/// <summary>US-6/FR-6 — new submission is always recorded regardless; this event flags it as a possible resubmission alongside an existing open submission.</summary>
public sealed record PotentialDuplicateSubmissionDetected(
    Guid SubmissionId,
    Guid SuspectedOriginalSubmissionId,
    string MatchBasis,
    decimal? ConfidenceLevel,
    DateTimeOffset DetectedAt);

/// <summary>US-7/FR-7 — underwriter/ops confirms the new submission is a genuine resubmission/update of the original. Appends to the original's own stream.</summary>
public sealed record SubmissionSuperseded(
    Guid OriginalSubmissionId,
    Guid SupersedingSubmissionId,
    string LinkedBy,
    DateTimeOffset LinkedAt);

/// <summary>US-8/FR-8 — underwriter/ops confirms the flagged submissions are coincidentally similar but genuinely distinct risks; both proceed independently.</summary>
public sealed record SubmissionConfirmedDistinct(
    Guid SubmissionId,
    Guid SuspectedOriginalSubmissionId,
    string ConfirmedBy,
    DateTimeOffset ConfirmedAt);

/// <summary>US-9/FR-9 — fires only on successful <see cref="SubmissionNormalized"/>. Rating computation is a specialist capability requested and displayed, never computed.</summary>
public sealed record BaselinePremiumGenerated(
    Guid SubmissionId,
    decimal BaselinePremium,
    string RiskFactorSummary,
    string ModelVersion,
    DateTimeOffset GeneratedAt);

/// <summary>US-10/FR-10 — fires alongside AssessSubmission when the underwriter's proposedTerms match <see cref="BaselinePremiumGenerated"/> exactly.</summary>
public sealed record PricingBaselineAccepted(
    Guid SubmissionId,
    decimal BaselinePremium,
    string UnderwriterId,
    DateTimeOffset AcceptedAt);

/// <summary>US-11/FR-11 — fires alongside AssessSubmission when the underwriter's proposedTerms diverge from the baseline.</summary>
public sealed record PricingBaselineOverridden(
    Guid SubmissionId,
    decimal BaselinePremium,
    decimal ProposedPremium,
    decimal Variance,
    string UnderwriterId,
    DateTimeOffset OverriddenAt);

/// <summary>US-12/FR-12 — audit reference data only. Referent for <see cref="BaselinePremiumGenerated"/>'s ModelVersion. Starts the separate PricingModel stream.</summary>
public sealed record PricingModelVersionDeployed(
    string ModelVersion,
    string DeployedBy,
    DateTimeOffset DeployedAt,
    string? ChangeSummary);
