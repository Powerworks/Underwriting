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

    /// <summary>
    /// Alias required by Marten: <c>options.Projections.Snapshot&lt;AuthorityLimit&gt;</c>
    /// (Module.cs) fails to close its internal generic types unless the identity
    /// member is literally named <c>Id</c> — the "{TypeName}Id" convention (which
    /// <see cref="AuthorityLimitId"/> follows and which document Query/LoadAsync
    /// does honor) is not enough for snapshot-projection registration specifically.
    /// Confirmed by reproducing `DocumentStore.For` throwing
    /// `ArgumentNullException` from `CloseAndBuildAs` with Marten 9.19.0 when this
    /// alias is absent. JsonIgnore keeps AuthorityLimitId as the one serialized/
    /// canonical field.
    /// </summary>
    [JsonIgnore]
    public Guid Id => AuthorityLimitId;

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

    /// <summary>
    /// Business revision counter (data-model.md), exposed on the wire as "version"
    /// on every response DTO/event — but deliberately NOT named "Version" on this
    /// class: Marten silently overwrites a document property literally named
    /// `Version` with its own internal optimistic-concurrency sequence number
    /// (confirmed empirically — after 1 event a property named `Version` reads
    /// back as 1 instead of the 0 this domain logic assigned it), corrupting this
    /// field's actual business meaning without any error or warning.
    /// </summary>
    public int RevisionNumber { get; private set; }

    [JsonConstructor]
    private AuthorityLimit(
        Guid authorityLimitId, string tier, string cellId, string? underwriterId,
        AuthorityScope scope, string? sourceAgreementReference, string grantedBy,
        DateTimeOffset grantedAt, string status, int revisionNumber)
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
        RevisionNumber = revisionNumber;
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
        RevisionNumber = @event.Version;
    }

    public void Apply(AuthorityLimitRevoked @event) => Status = "Revoked";

    // Rejections are recorded on this stream for audit (data-model.md Decision 3)
    // but never change this record's own state — Apply is a deliberate no-op.
    public void Apply(CellAuthorityLimitGrantRejected @event) { }
    public void Apply(UnderwriterAuthorityLimitRejected @event) { }
    public void Apply(AuthorityLimitRevisionRejected @event) { }
}
