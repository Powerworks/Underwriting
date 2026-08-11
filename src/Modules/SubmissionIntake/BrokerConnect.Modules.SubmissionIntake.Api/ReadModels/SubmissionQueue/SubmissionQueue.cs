using System.Text.Json.Serialization;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionQueue;

/// <summary>
/// Underwriter's main worklist (design.md Screens). Field list is the board's
/// SubmissionQueue field list (requirements.md Event Model Detail) plus
/// <see cref="NamedInsured"/>, a Technical Decisions gap-fix: the board's field
/// list omits it, but the "Insured" screen column needs it and the projector
/// already subscribes to <c>SubmissionNormalized</c>, which carries it.
/// </summary>
public sealed class SubmissionQueue
{
    public Guid SubmissionId { get; set; }

    // Marten's document-identity resolution requires the identity member to be
    // literally named Id (see Submission.Id's remarks in the Domain project for
    // the same constraint, confirmed the same way for 001's read models).
    [JsonIgnore]
    public Guid Id => SubmissionId;

    public string BrokerFirmId { get; set; } = string.Empty;

    public string NamedInsured { get; set; } = string.Empty;

    public string ClassOfBusiness { get; set; } = string.Empty;

    public string Territory { get; set; } = string.Empty;

    public decimal LineSizeSought { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public string Status { get; set; } = string.Empty;

    // Technical Decisions gap-fix: sourced from PotentialDuplicateSubmissionDetected /
    // SubmissionSuperseded / SubmissionConfirmedDistinct, wired into
    // SubmissionQueueProjector by task 5.5. Task 4.7 (this file) only creates/updates
    // the row from SubmissionNormalized, which has no bearing on duplicate status, so
    // both fields stay at their not-yet-flagged defaults (false/null) until then.
    public bool IsPossibleDuplicate { get; set; }

    public Guid? SuspectedOriginalSubmissionId { get; set; }
}
