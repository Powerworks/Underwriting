using System.Text.Json;
using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Wolverine.Http;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Commands.ReceiveBrokerSubmission;

public class ReceiveBrokerSubmissionHandler
{
    // TODO(ADR-010): [Authorize] policy deferred until the identity-provider
    // decision (Solution Arch §8, DEC-032) lands. Per design.md Security
    // Considerations, this is a system/service-to-service endpoint (scoped
    // API key/mTLS), not a user-shaped token, so no role policy applies here
    // regardless — flagged for the auth-story implementation to wire up.
    [WolverinePost("/api/v1/submission-intake/submissions")]
    public static async Task<Created<ReceiveBrokerSubmissionResponse>> Handle(
        ReceiveBrokerSubmissionRequest request,
        IDocumentSession session,
        ILogger<ReceiveBrokerSubmissionHandler> logger,
        CancellationToken cancellationToken)
    {
        var submissionId = Guid.NewGuid();
        var receivedAt = DateTimeOffset.UtcNow;

        // design.md Technical Decisions: raw payload storage is "reference,
        // not inline" — actual blob store is an unspecified implementation
        // detail. Until that store exists, the request payload is serialized
        // to a JSON string as the event's rawPayloadRef.
        var rawPayloadRef = JsonSerializer.Serialize(request.RawPayload);

        var received = new BrokerSubmissionReceived(
            submissionId,
            request.BrokerFirmId,
            request.SubmittingContact,
            rawPayloadRef,
            request.SourceChannel,
            receivedAt);

        session.Events.StartStream<Submission>(submissionId, received);
        await session.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Broker submission {SubmissionId} received from firm {BrokerFirmId} via {SourceChannel}",
            submissionId, request.BrokerFirmId, request.SourceChannel);

        return TypedResults.Created(
            $"/api/v1/submission-intake/submissions/{submissionId}",
            new ReceiveBrokerSubmissionResponse(submissionId, receivedAt));
    }
}
