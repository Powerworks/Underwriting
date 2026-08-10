using BrokerConnect.Modules.AuthorityAdministration.Domain.Events;
using Marten.Events.Projections;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.UnderwriterAuthorityRegister;

// T053 [US8]. Must be `partial` — same Marten source-generator requirement as
// CellAuthorityRegisterProjector.
public partial class UnderwriterAuthorityRegisterProjector
    : MultiStreamProjection<UnderwriterAuthorityRegister, string>
{
    public UnderwriterAuthorityRegisterProjector()
    {
        Identity<UnderwriterAuthorityLimitGranted>(e => e.UnderwriterId);
        Identity<CellAuthorityIncreaseRequested>(e => e.UnderwriterId);
        // No Identity<AuthorityLimitRevoked> — this register has no revocation
        // wiring at all (see UnderwriterAuthorityRegister's class remarks).
    }

    // Either event can legitimately be the first ever seen for a given
    // underwriterId: an escalation (CellAuthorityIncreaseRequested) follows a
    // *rejected* grant, so it can arrive with no prior UnderwriterAuthorityLimitGranted
    // at all.

    public static UnderwriterAuthorityRegister Create(UnderwriterAuthorityLimitGranted @event) => new()
    {
        UnderwriterId = @event.UnderwriterId,
        CellId = @event.CellId,
        CurrentScope = @event.Scope,
        CurrentVersion = @event.Version,
        EffectiveDate = @event.EffectiveDate,
        History = [new UnderwriterAuthorityRegisterHistoryEntry(
            "Grant", @event.AuthorityLimitId, @event.Version, @event.Scope, @event.EffectiveDate)],
    };

    public static UnderwriterAuthorityRegister Create(CellAuthorityIncreaseRequested @event) => new()
    {
        UnderwriterId = @event.UnderwriterId,
        CellId = @event.CellId,
        // CurrentScope/CurrentVersion/EffectiveDate stay null — nothing has been
        // granted yet, only requested.
        History = [new UnderwriterAuthorityRegisterHistoryEntry(
            "IncreaseRequested", @event.RequestId, version: null, @event.RequestedLimit,
            DateOnly.FromDateTime(@event.RequestedAt.UtcDateTime))],
    };

    public void Apply(UnderwriterAuthorityLimitGranted @event, UnderwriterAuthorityRegister register)
    {
        register.CurrentScope = @event.Scope;
        register.CurrentVersion = @event.Version;
        register.EffectiveDate = @event.EffectiveDate;
        register.History.Add(new UnderwriterAuthorityRegisterHistoryEntry(
            "Grant", @event.AuthorityLimitId, @event.Version, @event.Scope, @event.EffectiveDate));
    }

    // Never touches CurrentScope/CurrentVersion/EffectiveDate — data-model.md:
    // "an increase request is relevant history... even though it doesn't itself
    // change their limit."
    public void Apply(CellAuthorityIncreaseRequested @event, UnderwriterAuthorityRegister register)
    {
        register.History.Add(new UnderwriterAuthorityRegisterHistoryEntry(
            "IncreaseRequested", @event.RequestId, version: null, @event.RequestedLimit,
            DateOnly.FromDateTime(@event.RequestedAt.UtcDateTime)));
    }
}
