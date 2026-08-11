using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Wolverine.Http;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Commands.ConfirmSubmissionDistinct;

public class ConfirmSubmissionDistinctHandler
{
    // TODO(ADR-010): [Authorize] policy deferred until the identity-provider
    // decision (Solution Arch §8, DEC-032) lands — underwriter/ops role-gated
    // per design.md Security Considerations, same TODO pattern as 001's
    // GrantCellAuthorityLimitHandler.
    [WolverinePost("/api/v1/submission-intake/submissions/{submissionId}/confirm-distinct")]
    public static async Task<Results<Ok<ConfirmSubmissionDistinctResponse>, NotFound, ProblemHttpResult>> Handle(
        Guid submissionId,
        ConfirmSubmissionDistinctRequest request,
        IDocumentSession session,
        ILogger<ConfirmSubmissionDistinctHandler> logger,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<Submission>(submissionId, cancellationToken);
        if (stream.Aggregate is null)
        {
            logger.LogInformation("Confirm submission distinct {SubmissionId} failed: record not found", submissionId);
            return TypedResults.NotFound();
        }

        var entity = stream.Aggregate;

        // Idempotency (design.md Edge Cases): flag already cleared -- no-op,
        // return existing state rather than re-appending.
        if (entity.IsConfirmedDistinct)
        {
            return TypedResults.Ok(new ConfirmSubmissionDistinctResponse(
                submissionId, entity.SuspectedOriginalSubmissionId ?? Guid.Empty, DateTimeOffset.UtcNow));
        }

        if (entity.SuspectedOriginalSubmissionId is null)
        {
            logger.LogInformation(
                "Confirm submission distinct {SubmissionId} failed: submission is not flagged as a possible duplicate",
                submissionId);
            return TypedResults.Problem(
                title: "Submission is not flagged as a possible duplicate",
                statusCode: StatusCodes.Status409Conflict);
        }

        var confirmed = new SubmissionConfirmedDistinct(
            submissionId,
            entity.SuspectedOriginalSubmissionId.Value,
            request.ConfirmedBy,
            ConfirmedAt: DateTimeOffset.UtcNow);

        stream.AppendOne(confirmed);
        await session.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Submission {SubmissionId} confirmed distinct from {SuspectedOriginalSubmissionId}",
            submissionId, confirmed.SuspectedOriginalSubmissionId);

        return TypedResults.Ok(new ConfirmSubmissionDistinctResponse(
            submissionId, confirmed.SuspectedOriginalSubmissionId, confirmed.ConfirmedAt));
    }
}
