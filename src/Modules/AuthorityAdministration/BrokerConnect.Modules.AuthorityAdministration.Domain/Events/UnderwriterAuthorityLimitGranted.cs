namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

/// <summary>S0.2 — creates the AuthorityLimit stream, Tier = "Underwriter".</summary>
public sealed record UnderwriterAuthorityLimitGranted(
    Guid AuthorityLimitId,
    string UnderwriterId,
    string CellId,
    AuthorityScope Scope,
    string GrantedBy,
    DateOnly EffectiveDate,
    int Version,
    string ValidationResult,
    DateTimeOffset GrantedAt);
