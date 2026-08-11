using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Wolverine.Http;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Commands.CorrectSubmission;

public class CorrectSubmissionHandler
{
    // TODO(ADR-010): [Authorize] policy deferred until the identity-provider decision
    // (Solution Arch §8, DEC-032) lands -- ops/broker role-gated per design.md Security
    // Considerations, same TODO pattern as SupersedeSubmissionHandler (5.8).
    //
    // design.md Commands table: "does not itself re-normalize -- resubmittedForNormalization
    // flag is read by NormalizeSubmissionViaAdept (automation), per Principle II (no
    // downstream consequence inlined)". This handler only appends the event -- it never
    // calls IBrokerAdeptClient itself.
    [WolverinePost("/api/v1/submission-intake/submissions/{submissionId}/corrections")]
    public static async Task<Results<Ok<CorrectSubmissionResponse>, NotFound>> Handle(
        Guid submissionId,
        CorrectSubmissionRequest request,
        IDocumentSession session,
        ILogger<CorrectSubmissionHandler> logger,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<Submission>(submissionId, cancellationToken);
        if (stream.Aggregate is null)
        {
            logger.LogInformation("Correct submission {SubmissionId} failed: record not found", submissionId);
            return TypedResults.NotFound();
        }

        var corrected = new SubmissionManuallyCorrected(
            submissionId,
            request.CorrectedBy,
            request.CorrectionDescription,
            request.ResubmittedForNormalization,
            CorrectedAt: DateTimeOffset.UtcNow);

        stream.AppendOne(corrected);
        await session.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Submission {SubmissionId} manually corrected by {CorrectedBy} (resubmittedForNormalization={ResubmittedForNormalization})",
            submissionId, request.CorrectedBy, request.ResubmittedForNormalization);

        return TypedResults.Ok(new CorrectSubmissionResponse(
            submissionId, request.ResubmittedForNormalization, corrected.CorrectedAt));
    }
}
