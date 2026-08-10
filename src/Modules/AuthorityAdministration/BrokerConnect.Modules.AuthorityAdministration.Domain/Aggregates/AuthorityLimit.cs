using System.Text.Json.Serialization;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;

/// <summary>
/// Self-aggregating event stream, one stream per grant, keyed by AuthorityLimitId.
/// See specs/001-authority-administration/data-model.md for the full field/event
/// list and the rationale for each Apply overload (Decisions 2-3).
/// Apply mutates unconditionally, never guards its own preconditions — precondition
/// checks (duplicate-active, cascade, Revoked-terminal) live in the command handlers.
/// </summary>
public sealed class AuthorityLimit
{
    public Guid AuthorityLimitId { get; private init; }

    /// <summary>"Cell" | "Underwriter" — set once, at creation, never changes.</summary>
    public string Tier { get; private init; } = string.Empty;

    public string CellId { get; private init; } = string.Empty;

    /// <summary>Present only when Tier == "Underwriter".</summary>
    public string? UnderwriterId { get; private init; }

    public AuthorityScope Scope { get; private set; } = null!;

    public string? SourceAgreementReference { get; private init; }

    public string GrantedBy { get; private init; } = string.Empty;

    public DateTimeOffset GrantedAt { get; private init; }

    /// <summary>"Active" | "Revoked" (Clarified 2026-08-09) — Revoked is terminal.</summary>
    public string Status { get; private set; } = "Active";

    public int Version { get; private set; }

    [JsonConstructor]
    private AuthorityLimit(
        Guid authorityLimitId, string tier, string cellId, string? underwriterId,
        AuthorityScope scope, string? sourceAgreementReference, string grantedBy,
        DateTimeOffset grantedAt, string status, int version)
    {
        AuthorityLimitId = authorityLimitId;
        Tier = tier;
        CellId = cellId;
        UnderwriterId = underwriterId;
        Scope = scope;
        SourceAgreementReference = sourceAgreementReference;
        GrantedBy = grantedBy;
        GrantedAt = grantedAt;
        Status = status;
        Version = version;
    }

    public static AuthorityLimit Create(CellAuthorityLimitGranted @event) => new(
        @event.AuthorityLimitId, tier: "Cell", @event.CellId, underwriterId: null,
        @event.AuthorityScope, @event.SourceAgreementReference, @event.GrantedBy,
        @event.GrantedAt, status: "Active", @event.Version);

    public static AuthorityLimit Create(UnderwriterAuthorityLimitGranted @event) => new(
        @event.AuthorityLimitId, tier: "Underwriter", @event.CellId, @event.UnderwriterId,
        @event.Scope, sourceAgreementReference: null, @event.GrantedBy,
        @event.GrantedAt, status: "Active", @event.Version);

    public void Apply(AuthorityLimitRevised @event)
    {
        Scope = @event.NewLimit;
        Version = @event.Version;
    }

    public void Apply(AuthorityLimitRevoked @event) => Status = "Revoked";

    // Rejections are recorded on this stream for audit (data-model.md Decision 3)
    // but never change this record's own state — Apply is a deliberate no-op.
    public void Apply(CellAuthorityLimitGrantRejected @event) { }
    public void Apply(UnderwriterAuthorityLimitRejected @event) { }
    public void Apply(AuthorityLimitRevisionRejected @event) { }
}
