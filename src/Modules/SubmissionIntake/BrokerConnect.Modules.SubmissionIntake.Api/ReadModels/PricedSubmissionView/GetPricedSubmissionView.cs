using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Wolverine.Http;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.PricedSubmissionView;

public static class GetPricedSubmissionView
{
    // Route inferred, consistent convention (mirrors AuthorityAdministration's
    // GetAuthorityMatrix single-item-by-id shape) -- not board-specified (design.md
    // has no Screens entry transcribing this exact route, but the file-tree entry
    // (Read Models table) confirms the endpoint exists). Single-item lookup, not a
    // paginated list like 4.8/4.10/6.7 -- "before the underwriter opens the file"
    // (requirements.md narrative) implies a per-submission view, not a queue.
    // Constitution Principle VII: returns a DTO, never the Submission aggregate itself
    // (design.md's own note: "never the aggregate").
    [WolverineGet("/api/v1/submission-intake/submissions/{submissionId}/priced-view")]
    public static async Task<Results<Ok<PricedSubmissionViewResponse>, NotFound>> Handle(
        Guid submissionId,
        IQuerySession session,
        ILogger<PricedSubmissionView> logger,
        CancellationToken cancellationToken)
    {
        var view = await session.LoadAsync<PricedSubmissionView>(submissionId, cancellationToken);
        if (view is null)
        {
            logger.LogDebug("PricedSubmissionView not found for {SubmissionId}", submissionId);
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new PricedSubmissionViewResponse(
            view.SubmissionId, view.BrokerRequestedTerms, view.BaselinePremium,
            view.RiskFactorSummary, view.ModelVersion));
    }
}

public sealed record PricedSubmissionViewResponse(
    Guid SubmissionId, string? BrokerRequestedTerms, decimal? BaselinePremium,
    string? RiskFactorSummary, string? ModelVersion);
