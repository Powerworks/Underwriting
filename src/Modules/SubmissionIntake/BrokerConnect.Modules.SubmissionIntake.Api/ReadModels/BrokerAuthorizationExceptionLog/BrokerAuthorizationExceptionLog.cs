using System.Text.Json.Serialization;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.BrokerAuthorizationExceptionLog;

/// <summary>
/// Ops/broker-relationship-management's log of broker-panel-authorization exceptions
/// (design.md Overview) -- "never appears in any underwriter's queue" (requirements.md
/// Event Model Detail). Field list is the board's BrokerAuthorizationExceptionLog field
/// list: submissionId, brokerFirmId, requestedCellId, requestedClassOfBusiness,
/// rejectionReason, rejectedAt, reviewStatus.
/// </summary>
public sealed class BrokerAuthorizationExceptionLog
{
    public Guid SubmissionId { get; set; }

    // Marten's document-identity resolution requires the identity member to be
    // literally named Id (see Submission.Id's remarks in the Domain project; same
    // constraint applied to SubmissionQueue/SubmissionExceptionQueue in tasks 4.7/4.9).
    [JsonIgnore]
    public Guid Id => SubmissionId;

    public string BrokerFirmId { get; set; } = string.Empty;

    public string RequestedCellId { get; set; } = string.Empty;

    public string? RequestedClassOfBusiness { get; set; }

    public string RejectionReason { get; set; } = string.Empty;

    public DateTimeOffset RejectedAt { get; set; }

    // Task 6.6 field-tracing: SubmissionRoutingRejected carries no reviewStatus field
    // (requirements.md Event Model Detail) -- every other field on this read model maps
    // 1:1 onto that event, so reviewStatus is the only one with no event source at all.
    // Set to the "Pending" literal on creation (mirrors SubmissionExceptionQueue.Status's
    // "Failed" literal from task 4.9 -- no new enum invented), since ops hasn't reviewed
    // the exception yet at the moment it's recorded.
    public string ReviewStatus { get; set; } = string.Empty;
}
