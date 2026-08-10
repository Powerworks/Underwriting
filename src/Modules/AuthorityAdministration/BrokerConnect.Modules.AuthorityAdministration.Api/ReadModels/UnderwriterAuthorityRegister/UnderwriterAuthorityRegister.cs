using System.Text.Json.Serialization;
using BrokerConnect.Modules.AuthorityAdministration.Domain;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.UnderwriterAuthorityRegister;

/// <summary>
/// Historical register per underwriter (data-model.md: "same shape as
/// CellAuthorityRegister"), a plain async Marten projection. Multi-stream, keyed
/// by <c>underwriterId</c> — see <see cref="UnderwriterAuthorityRegisterProjector"/>.
/// </summary>
/// <remarks>
/// Two deliberate divergences from a literal "same shape as CellAuthorityRegister"
/// reading, both because this register's actual source events don't behave like
/// CellAuthorityRegister's do:
/// <list type="bullet">
/// <item><c>CurrentScope</c>/<c>CurrentVersion</c>/<c>EffectiveDate</c> are nullable
/// here, not required. The board's own dependency edges for this read model
/// (spec.md) wire <c>CellAuthorityIncreaseRequested</c> as a source — and that
/// event fires precisely when a grant was <em>rejected</em> (US3's rejection path),
/// meaning an escalation can be the very first event this register ever sees for a
/// given underwriter, with no grant, and therefore no "current" authority, yet.</item>
/// <item>No <c>AuthorityLimitRevoked</c> wiring exists for this register at all
/// (unlike <c>CellAuthorityRegister</c>) — confirmed against spec.md's board export,
/// not inferred. <c>SupersededAt</c> is carried on history entries for field-shape
/// parity with <c>CellAuthorityRegister</c>, but nothing wired here will ever
/// populate it; it will always be null.</item>
/// </list>
/// </remarks>
public sealed class UnderwriterAuthorityRegister
{
    public string UnderwriterId { get; set; } = string.Empty;

    // Same Marten registration requirement as AuthorityLimit.Id / CellAuthorityRegister.Id.
    [JsonIgnore]
    public string Id => UnderwriterId;

    public string CellId { get; set; } = string.Empty;
    public AuthorityScope? CurrentScope { get; set; }
    public int? CurrentVersion { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public List<UnderwriterAuthorityRegisterHistoryEntry> History { get; set; } = [];
}

/// <summary>
/// <c>Kind</c>/<c>SourceId</c> are additions beyond data-model.md's literal
/// <c>{version, scope, effectiveDate, supersededAt?}</c> shape — needed because a
/// grant and an increase *request* don't map onto the same fields the same way
/// (a request has no grant version, and its "scope" is what was asked for, not
/// what's in force). <c>Kind</c> lets a caller tell the two apart; <c>SourceId</c>
/// (the granting stream's <c>authorityLimitId</c>, or the request's own
/// <c>requestId</c>) is for traceability back to the source record, not used for
/// any matching logic here (unlike CellAuthorityRegister's <c>AuthorityLimitId</c>,
/// which matching a Revoked event does need — no revocation is wired to this
/// register at all, see the class remarks).
/// </summary>
public sealed class UnderwriterAuthorityRegisterHistoryEntry(
    string kind, Guid sourceId, int? version, AuthorityScope scope, DateOnly effectiveDate)
{
    public string Kind { get; } = kind; // "Grant" | "IncreaseRequested"
    public Guid SourceId { get; } = sourceId;
    public int? Version { get; } = version;
    public AuthorityScope Scope { get; } = scope;
    public DateOnly EffectiveDate { get; } = effectiveDate;
    public DateTimeOffset? SupersededAt { get; set; }
}
