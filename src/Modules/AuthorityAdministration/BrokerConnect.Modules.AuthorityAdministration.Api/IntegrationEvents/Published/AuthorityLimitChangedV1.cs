namespace BrokerConnect.Modules.AuthorityAdministration.Api.IntegrationEvents.Published;

/// <summary>
/// Published (via the WolverineFx.Marten outbox, same transaction) whenever
/// AuthorityLimitRevised or AuthorityLimitRevoked is appended. Consumed by
/// 004-underwriting-decisioning's own ReassessInFlightSubmissionsOnRuleChange
/// automation — see data-model.md "Cross-module boundary" and research.md
/// Decision 4. This module's scope ends at publishing; it never implements the
/// reassessment itself (ADR-004 module isolation).
/// </summary>
public sealed record AuthorityLimitChangedV1(
    Guid AuthorityLimitId,
    string ChangeType, // "Revised" | "Revoked"
    string CellId,
    string? UnderwriterId,
    DateTimeOffset ChangedAt);
