namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

/// <summary>
/// S0.3 follow-on — creates the CellAuthorityIncreaseRequest stream. The request
/// itself is the auditable fact; no approval/denial lifecycle is modeled by the
/// source board yet (data-model.md).
/// </summary>
/// <remarks>
/// <c>UnderwriterId</c> is a (new) field not on the original board export: the
/// board's own dependency edge wires this event into <c>UnderwriterAuthorityRegister</c>
/// (spec.md's Event Model Detail appendix — "→ UnderwriterAuthorityRegister
/// (READMODEL)"), keyed by <c>underwriterId</c>, but neither this event nor
/// <c>RequestCellAuthorityIncrease</c>'s route/request ever captured which
/// underwriter the escalation was for — even though the narrative is explicitly
/// "escalation path from [a specific underwriter's] rejection." Without it, the
/// board's declared edge has no field to route by at all (not just an
/// incompleteness, an impossibility). The underwriter is always known in the real
/// flow (the caller is escalating one specific rejected grant), so this is a
/// required field, not optional — confirmed with the user before widening an
/// already-shipped US7 contract for a different feature's (US8) sake.
/// </remarks>
public sealed record CellAuthorityIncreaseRequested(
    Guid RequestId,
    string CellId,
    string UnderwriterId,
    string RequestedBy,
    AuthorityScope CurrentLimit,
    AuthorityScope RequestedLimit,
    string Justification,
    DateTimeOffset RequestedAt);
