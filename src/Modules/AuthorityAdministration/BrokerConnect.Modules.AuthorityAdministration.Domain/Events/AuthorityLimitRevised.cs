namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

/// <summary>S0.4 — appends to the existing stream, increments Version.</summary>
public sealed record AuthorityLimitRevised(
    Guid AuthorityLimitId,
    string Target,
    AuthorityScope PreviousLimit,
    AuthorityScope NewLimit,
    string Reason,
    string RevisedBy,
    DateOnly EffectiveDate,
    int Version,
    DateTimeOffset RevisedAt);
