using System.Text.Json;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.PricedSubmissionView;

// design.md Read Models: PricedSubmissionView projector, Wolverine-subscriber pattern
// per Existing Patterns to Follow -- keyed by submissionId, which is the Submission
// stream's own id, so no MultiStreamProjection is needed. Wolverine.Marten's
// IntegrateWithWolverine() forwards captured events to matching static Handle methods
// automatically (see Api.Host/Program.cs), so no explicit subscription wiring is
// needed here.
//
// design.md's Read Models table lists 3 source events for this projector:
// BaselinePremiumGenerated, SubmissionConfirmedDistinct (both board-wired per
// requirements.md's Dependencies list), and the gap-fixed SubmissionNormalized (for
// brokerRequestedTerms -- Technical Decisions: "(b) populate from normalized fields").
public sealed class PricedSubmissionViewProjector
{
    public static async Task Handle(
        BaselinePremiumGenerated @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        var view = await session.LoadAsync<PricedSubmissionView>(@event.SubmissionId, cancellationToken)
            ?? new PricedSubmissionView { SubmissionId = @event.SubmissionId };

        view.BaselinePremium = @event.BaselinePremium;
        view.RiskFactorSummary = @event.RiskFactorSummary;
        view.ModelVersion = @event.ModelVersion;

        session.Store(view);
        await session.SaveChangesAsync(cancellationToken);
    }

    // Gap-fix (design.md Technical Decisions): brokerRequestedTerms has no board-listed
    // event dependency; SubmissionNormalized's structured fields are the only plausible
    // source for "the broker's ask". JSON-serialized, same treatment as
    // BaselinePricingResult.RiskFactorSummary (7.5).
    public static async Task Handle(
        SubmissionNormalized @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        var view = await session.LoadAsync<PricedSubmissionView>(@event.SubmissionId, cancellationToken)
            ?? new PricedSubmissionView { SubmissionId = @event.SubmissionId };

        view.BrokerRequestedTerms = JsonSerializer.Serialize(new
        {
            @event.ClassOfBusiness,
            @event.Territory,
            @event.NamedInsured,
            @event.LineSizeSought,
            @event.KeyTerms,
            @event.EffectiveDateRequested,
        });

        session.Store(view);
        await session.SaveChangesAsync(cancellationToken);
    }

    // Board-wired dependency (requirements.md: "← SubmissionConfirmedDistinct (EVENT)")
    // with no field of its own on this read model -- none of PricedSubmissionView's
    // fields plausibly derive from a duplicate-resolution outcome. Ensures the row
    // exists (upsert, same LoadAsync-or-create shape as the other Handle methods here)
    // so the view is available for a submission the moment it's confirmed distinct,
    // even if BaselinePremiumGenerated/SubmissionNormalized haven't landed yet -- rather
    // than silently ignoring a real board-modeled dependency edge.
    public static async Task Handle(
        SubmissionConfirmedDistinct @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        var view = await session.LoadAsync<PricedSubmissionView>(@event.SubmissionId, cancellationToken)
            ?? new PricedSubmissionView { SubmissionId = @event.SubmissionId };

        session.Store(view);
        await session.SaveChangesAsync(cancellationToken);
    }
}
