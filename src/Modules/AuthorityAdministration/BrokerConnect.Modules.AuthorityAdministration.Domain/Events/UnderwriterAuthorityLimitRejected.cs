namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

/// <summary>
/// S0.3, extended 2026-08-09 (data-model.md Decision 3): RejectionReason is now
/// "ExceedsCellLimit" (original) or "DuplicateActiveGrant" (FR-011, new). Appends
/// to the Cell's own stream — the entity the rejection was evaluated against.
/// </summary>
public sealed record UnderwriterAuthorityLimitRejected(
    Guid AuthorityLimitId,
    string UnderwriterId,
    string CellId,
    AuthorityScope RequestedScope,
    string RejectionReason,
    string AttemptedBy,
    DateTimeOffset RejectedAt);
