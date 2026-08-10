namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

/// <summary>S0.1 — creates the AuthorityLimit stream, Tier = "Cell".</summary>
public sealed record CellAuthorityLimitGranted(
    Guid AuthorityLimitId,
    string CellId,
    AuthorityScope AuthorityScope,
    string SourceAgreementReference,
    string GrantedBy,
    DateOnly EffectiveDate,
    int Version,
    DateTimeOffset GrantedAt);
