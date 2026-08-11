using System.Text.Json.Serialization;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionExceptionQueue;

/// <summary>
/// Ops' worklist for submissions stuck in a failed normalization state (design.md
/// Overview). Field list is the board's SubmissionExceptionQueue field list
/// (requirements.md Event Model Detail): submissionId, brokerFirmId,
/// failureReason, attemptedAt, status.
/// </summary>
public sealed class SubmissionExceptionQueue
{
    public Guid SubmissionId { get; set; }

    // Marten's document-identity resolution requires the identity member to be
    // literally named Id (see Submission.Id's remarks in the Domain project;
    // same constraint applied to SubmissionQueue in task 4.7).
    [JsonIgnore]
    public Guid Id => SubmissionId;

    // Task 4.9: the board's SubmissionNormalizationFailed event carries no
    // brokerFirmId, so this field can only come from BrokerSubmissionReceived
    // (per the 4.7.1 lesson -- cross-checked against every field's real source
    // event rather than assuming the single named trigger event covers them all).
    public string BrokerFirmId { get; set; } = string.Empty;

    public string FailureReason { get; set; } = string.Empty;

    public DateTimeOffset AttemptedAt { get; set; }

    // Set to "Failed" when SubmissionNormalizationFailed is projected -- mirrors
    // the literal Submission.Apply(SubmissionNormalizationFailed) already uses for
    // the write-side NormalizationStatus (no new enum invented). Design.md's
    // Technical Decisions table notes this field otherwise never changes off its
    // initial value until SubmissionManuallyCorrected is wired in task 8.5.
    public string Status { get; set; } = string.Empty;
}
