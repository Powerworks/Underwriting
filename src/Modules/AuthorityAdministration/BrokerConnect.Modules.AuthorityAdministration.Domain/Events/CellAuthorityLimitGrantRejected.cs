namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

/// <summary>
/// New (data-model.md Decision 3, Clarified 2026-08-09 / FR-011). Appends to the
/// *existing* Active Cell-tier stream being duplicated — this rejection is never
/// the first event on a stream.
/// </summary>
public sealed record CellAuthorityLimitGrantRejected(
    Guid AuthorityLimitId,
    string AttemptedCellId,
    AuthorityScope AttemptedScope,
    string RejectionReason,
    string AttemptedBy,
    DateTimeOffset RejectedAt);
