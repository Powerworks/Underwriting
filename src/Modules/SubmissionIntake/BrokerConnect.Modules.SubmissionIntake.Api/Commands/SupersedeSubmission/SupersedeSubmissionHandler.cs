using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Wolverine.Http;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Commands.SupersedeSubmission;

public class SupersedeSubmissionHandler
{
    // TODO(ADR-010): [Authorize] policy deferred until the identity-provider
    // decision (Solution Arch §8, DEC-032) lands — underwriter/ops role-gated
    // per design.md Security Considerations, same TODO pattern as 001's
    // GrantCellAuthorityLimitHandler.
    [WolverinePost("/api/v1/submission-intake/submissions/{originalSubmissionId}/supersede")]
    public static async Task<Results<Ok<SupersedeSubmissionResponse>, NotFound>> Handle(
        Guid originalSubmissionId,
        SupersedeSubmissionRequest request,
        IDocumentSession session,
        ILogger<SupersedeSubmissionHandler> logger,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<Submission>(originalSubmissionId, cancellationToken);
        if (stream.Aggregate is null)
        {
            logger.LogInformation("Supersede submission {OriginalSubmissionId} failed: record not found", originalSubmissionId);
            return TypedResults.NotFound();
        }

        var entity = stream.Aggregate;

        // Idempotency (design.md Edge Cases): already superseded by this same
        // submission -- no-op, return existing state rather than re-appending.
        if (entity.SupersededBySubmissionId == request.SupersedingSubmissionId)
        {
            return TypedResults.Ok(new SupersedeSubmissionResponse(
                originalSubmissionId, request.SupersedingSubmissionId, DateTimeOffset.UtcNow));
        }

        var superseded = new SubmissionSuperseded(
            originalSubmissionId,
            request.SupersedingSubmissionId,
            request.LinkedBy,
            LinkedAt: DateTimeOffset.UtcNow);

        stream.AppendOne(superseded);
        await session.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Submission {OriginalSubmissionId} superseded by {SupersedingSubmissionId}",
            originalSubmissionId, request.SupersedingSubmissionId);

        return TypedResults.Ok(new SupersedeSubmissionResponse(
            originalSubmissionId, request.SupersedingSubmissionId, superseded.LinkedAt));
    }
}
