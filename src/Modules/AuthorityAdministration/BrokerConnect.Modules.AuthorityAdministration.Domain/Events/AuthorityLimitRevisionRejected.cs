namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

/// <summary>
/// New (data-model.md Decision 3, Clarified 2026-08-09 / FR-010, FR-012).
/// RejectionReason is "RevokedRecord" or "ExceedsCellLimit". Appends to the
/// target record's own stream — the one ReviseAuthorityLimit was trying to revise.
/// </summary>
public sealed record AuthorityLimitRevisionRejected(
    Guid AuthorityLimitId,
    AuthorityScope AttemptedNewLimit,
    string RejectionReason,
    string AttemptedBy,
    DateTimeOffset RejectedAt);
