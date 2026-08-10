namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

/// <summary>S0.5 — terminal. Apply sets Status = "Revoked".</summary>
public sealed record AuthorityLimitRevoked(
    Guid AuthorityLimitId,
    string Target,
    AuthorityScope PriorLimit,
    string Reason,
    string RevokedBy,
    string Immediacy,
    DateTimeOffset RevokedAt);
