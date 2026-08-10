namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

/// <summary>
/// S0.3 follow-on — creates the CellAuthorityIncreaseRequest stream. The request
/// itself is the auditable fact; no approval/denial lifecycle is modeled by the
/// source board yet (data-model.md).
/// </summary>
public sealed record CellAuthorityIncreaseRequested(
    Guid RequestId,
    string CellId,
    string RequestedBy,
    AuthorityScope CurrentLimit,
    AuthorityScope RequestedLimit,
    string Justification,
    DateTimeOffset RequestedAt);
