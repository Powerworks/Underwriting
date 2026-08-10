using BrokerConnect.Modules.AuthorityAdministration.Domain.Events;
using Marten.Events.Projections;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.CellAuthorityRegister;

// T050 [US6]. Must be `partial` — Marten's JasperFx.Events source generator
// dispatches Create/Apply convention methods for projection subclasses at compile
// time (confirmed the same requirement earlier this project for the AuthorityLimit
// snapshot registration; a non-partial projection subclass throws
// InvalidProjectionException at store build time with no runtime fallback).
public partial class CellAuthorityRegisterProjector : MultiStreamProjection<CellAuthorityRegister, string>
{
    public CellAuthorityRegisterProjector()
    {
        Identity<CellAuthorityLimitGranted>(e => e.CellId);
        Identity<AuthorityLimitRevoked>(e => e.CellId);
    }

    public static CellAuthorityRegister Create(CellAuthorityLimitGranted @event) => new()
    {
        CellId = @event.CellId,
        CurrentScope = @event.AuthorityScope,
        CurrentVersion = @event.Version,
        EffectiveDate = @event.EffectiveDate,
        History = [new CellAuthorityRegisterHistoryEntry(
            @event.AuthorityLimitId, @event.Version, @event.AuthorityScope, @event.EffectiveDate)],
    };

    // A cell can hold more than one Active AuthorityLimit stream at once (different
    // classes of business, or a fresh grant after an earlier one was revoked) — each
    // is its own history entry; "current" reflects whichever grant landed most
    // recently, since this register has no per-class-of-business breakdown
    // (data-model.md's shape is a single currentScope/currentVersion, not a list).
    public void Apply(CellAuthorityLimitGranted @event, CellAuthorityRegister register)
    {
        register.CurrentScope = @event.AuthorityScope;
        register.CurrentVersion = @event.Version;
        register.EffectiveDate = @event.EffectiveDate;
        register.History.Add(new CellAuthorityRegisterHistoryEntry(
            @event.AuthorityLimitId, @event.Version, @event.AuthorityScope, @event.EffectiveDate));
    }

    // Underwriter-tier revocations also carry a CellId (the underwriter's own cell)
    // and so are routed to this same document by Identity<AuthorityLimitRevoked>
    // above, but they don't belong in a CELL register — no-op, matching this
    // codebase's established pattern for events that route to a document without
    // mutating it (see AuthorityLimit.Apply(*Rejected) no-ops).
    public void Apply(AuthorityLimitRevoked @event, CellAuthorityRegister register)
    {
        if (@event.Target != "Cell") return;

        var entry = register.History.FirstOrDefault(h => h.AuthorityLimitId == @event.AuthorityLimitId);
        if (entry is not null) entry.SupersededAt = @event.RevokedAt;
    }
}
