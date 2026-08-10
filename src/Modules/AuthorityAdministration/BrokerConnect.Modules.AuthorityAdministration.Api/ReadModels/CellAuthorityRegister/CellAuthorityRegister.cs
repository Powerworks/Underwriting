using System.Text.Json.Serialization;
using BrokerConnect.Modules.AuthorityAdministration.Domain;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.CellAuthorityRegister;

/// <summary>
/// Historical register per cell (data-model.md), a plain async Marten projection
/// (not a snapshot — no same-request consistency requirement, per plan.md's
/// Architecture Constraints check). Multi-stream: <see cref="CellAuthorityLimitGranted"/>/
/// <see cref="AuthorityLimitRevoked"/> live on the <c>AuthorityLimit</c> stream
/// (keyed by <c>authorityLimitId</c>), but this document is keyed by <c>cellId</c>.
/// </summary>
/// <remarks>
/// The board's own dependency edges for this read model list only
/// <c>CellAuthorityLimitGranted</c> and <c>AuthorityLimitRevoked</c> as sources — not
/// <c>AuthorityLimitRevised</c> (data-model.md / spec.md FR-006), even though the
/// <c>history</c> field shape implies revision tracking. That's the literal,
/// consistently-documented source spec (unlike the AuthorityMatrix board-edge gap
/// fixed earlier this project), so it's implemented as specified rather than
/// silently extended: a grant later revised (not revoked) will not show that
/// revision here. Flagging, not fixing — this is a real limitation of what this
/// register can show today.
/// </remarks>
public sealed class CellAuthorityRegister
{
    public string CellId { get; set; } = string.Empty;

    // Marten's Snapshot/multi-stream document-identity resolution requires the
    // identity member to be literally named Id (see AuthorityLimit.Id's remarks —
    // the same constraint applies here, confirmed the same way).
    [JsonIgnore]
    public string Id => CellId;

    public AuthorityScope CurrentScope { get; set; } = null!;
    public int CurrentVersion { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public List<CellAuthorityRegisterHistoryEntry> History { get; set; } = [];
}

/// <summary>
/// <c>authorityLimitId</c> is not in data-model.md's history entry shape
/// (<c>{version, scope, effectiveDate, supersededAt?}</c>) — added because without it
/// there is no way to match an <c>AuthorityLimitRevoked</c> event back to the specific
/// history entry it supersedes once a cell has more than one grant (e.g. different
/// classes of business).
/// </summary>
public sealed class CellAuthorityRegisterHistoryEntry(
    Guid authorityLimitId, int version, AuthorityScope scope, DateOnly effectiveDate)
{
    public Guid AuthorityLimitId { get; } = authorityLimitId;
    public int Version { get; } = version;
    public AuthorityScope Scope { get; } = scope;
    public DateOnly EffectiveDate { get; } = effectiveDate;
    public DateTimeOffset? SupersededAt { get; set; }
}
