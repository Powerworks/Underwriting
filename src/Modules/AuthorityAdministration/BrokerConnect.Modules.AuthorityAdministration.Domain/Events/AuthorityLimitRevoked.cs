namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

/// <summary>S0.5 — terminal. Apply sets Status = "Revoked".</summary>
/// <remarks>
/// <c>CellId</c> is a (new) field not on the original board export: the board's own
/// dependency edges wire this event into <c>CellAuthorityRegister</c> (data-model.md/
/// spec.md FR-006 — Dependencies: "← AuthorityLimitRevoked (EVENT)"), but that register
/// is keyed by <c>cellId</c>, and this event's own stream is keyed by
/// <c>authorityLimitId</c>. Without carrying <c>CellId</c> here, a cross-stream
/// projection has no way to route the event to the right register without an extra
/// lookup per event — the value is already in scope at handler-time
/// (<c>entity.CellId</c>, the same source <c>RevokeAuthorityLimitHandler</c> already
/// reads to build <c>AuthorityLimitChangedV1</c>), so carrying it here is a minimal,
/// justified addition rather than the kind of unrequested field this codebase
/// otherwise avoids inventing.
/// </remarks>
public sealed record AuthorityLimitRevoked(
    Guid AuthorityLimitId,
    string Target,
    string CellId,
    AuthorityScope PriorLimit,
    string Reason,
    string RevokedBy,
    string Immediacy,
    DateTimeOffset RevokedAt);
