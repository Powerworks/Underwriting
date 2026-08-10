using BrokerConnect.Modules.SubmissionIntake.Api.Commands.ReceiveBrokerSubmission;
using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Marten;
using Marten.Events;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace SubmissionIntake.Api.Tests;

// T3.1 [US-1] AC-1.1: "always succeeds if the payload arrives at all" — no
// validation beyond [Required] field presence on the request itself, so this
// is a Layer 2 test (NSubstitute IDocumentSession) rather than Layer 3: unlike
// 001's FetchForWriting/Query<T> handlers, this handler only calls
// Events.StartStream + SaveChangesAsync, which NSubstitute can verify as a
// plain "was this call made" check (same shape as 001's IMessageBus mocking),
// without needing a real read-after-write round trip.
public class ReceiveBrokerSubmissionHandlerTests
{
    [Fact]
    public async Task Well_formed_request_always_succeeds_and_starts_the_Submission_stream()
    {
        var session = Substitute.For<IDocumentSession>();
        var events = Substitute.For<IEventStoreOperations>();
        session.Events.Returns(events);

        var request = new ReceiveBrokerSubmissionRequest(
            "acme-brokers", "jane@acme.com", CellIdHint: null, ClassOfBusinessHint: null,
            RawPayload: new { insured = "Acme Corp" }, SourceChannel: "api");

        var result = await ReceiveBrokerSubmissionHandler.Handle(
            request, session, NullLogger<ReceiveBrokerSubmissionHandler>.Instance, CancellationToken.None);

        var created = result.ShouldBeOfType<Created<ReceiveBrokerSubmissionResponse>>();
        created.Value!.SubmissionId.ShouldNotBe(Guid.Empty);

        events.Received(1).StartStream<Submission>(
            created.Value.SubmissionId,
            Arg.Is<object[]>(appended =>
                appended.Length == 1 &&
                appended[0] is BrokerSubmissionReceived received &&
                received.SubmissionId == created.Value.SubmissionId &&
                received.BrokerFirmId == request.BrokerFirmId &&
                received.SubmittingContact == request.SubmittingContact &&
                received.SourceChannel == request.SourceChannel));

        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
