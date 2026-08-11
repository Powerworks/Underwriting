using BrokerConnect.Modules.SubmissionIntake.Api.Automations.NormalizeSubmissionViaAdept;
using BrokerConnect.Modules.SubmissionIntake.Api.Commands.CorrectSubmission;
using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using BrokerConnect.Modules.SubmissionIntake.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace SubmissionIntake.IntegrationTests;

// 8.3 -- Layer 3 (Testcontainers-backed Postgres, real FetchForWriting/AggregateStreamAsync),
// same shared fixture as 4.4/5.7 (SubmissionIntakePostgresFixture). Per design.md Commands
// table: CorrectSubmission -> POST .../{submissionId}/corrections -> SubmissionManuallyCorrected
// -- "does not itself re-normalize -- resubmittedForNormalization flag is read by
// NormalizeSubmissionViaAdept (automation), per Principle II". This project's Layer 3
// convention calls handler methods directly rather than through a running Wolverine host
// (matching every other automation test in this suite), so "re-triggers" is exercised by
// calling NormalizeSubmissionViaAdeptHandler.Handle directly with the correction event, the
// same way Wolverine's event-forwarding would dispatch it.
//
// RED: CorrectSubmissionHandler does not exist yet, and NormalizeSubmissionViaAdeptHandler's
// SubmissionManuallyCorrected overload is still the inert 4.5 stub (2-arg signature, no
// session/client params) -- both created/extended in 8.4. This file is expected to fail to
// compile until then -- that is the correct RED state.
[Collection(SubmissionIntakePostgresCollection.Name)]
public class CorrectSubmissionHandlerTests(SubmissionIntakePostgresFixture fixture)
{
    private static BrokerSubmissionReceived ReceivedEvent(Guid submissionId, string rawPayloadRef) =>
        new(submissionId, "acme-brokers", "jane@acme.com", rawPayloadRef, "api", DateTimeOffset.UtcNow);

    // (1) CorrectSubmission appends SubmissionManuallyCorrected on the submission's own stream.
    [Fact]
    public async Task CorrectSubmission_appends_SubmissionManuallyCorrected()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        session.Events.StartStream<Submission>(submissionId, ReceivedEvent(submissionId, $"raw-payload-{submissionId:N}"));
        await session.SaveChangesAsync();

        var request = new CorrectSubmissionRequest("ops-jane", "Fixed missing class code", ResubmittedForNormalization: false);

        var result = await CorrectSubmissionHandler.Handle(
            submissionId, request, session, NullLogger<CorrectSubmissionHandler>.Instance, CancellationToken.None);

        result.ShouldNotBeNull();

        var events = await session.Events.FetchStreamAsync(submissionId);
        var corrected = events.Select(e => e.Data).OfType<SubmissionManuallyCorrected>().Single();
        corrected.CorrectedBy.ShouldBe("ops-jane");
        corrected.CorrectionDescription.ShouldBe("Fixed missing class code");
        corrected.ResubmittedForNormalization.ShouldBeFalse();
    }

    // (2) resubmittedForNormalization == true -> NormalizeSubmissionViaAdeptHandler
    // (already subscribed to SubmissionManuallyCorrected since 4.5) re-normalizes,
    // overriding a prior SubmissionNormalizationFailed.
    [Fact]
    public async Task Resubmitted_for_normalization_true_re_triggers_normalization()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var rawPayloadRef = $"raw-payload-{submissionId:N}";
        session.Events.StartStream<Submission>(submissionId, ReceivedEvent(submissionId, rawPayloadRef));
        session.Events.Append(submissionId, new SubmissionNormalizationFailed(
            submissionId, "MissingClassCode", rawPayloadRef, DateTimeOffset.UtcNow));
        await session.SaveChangesAsync();

        var corrected = new SubmissionManuallyCorrected(
            submissionId, "ops-jane", "Added the missing class code", true, DateTimeOffset.UtcNow);
        session.Events.Append(submissionId, corrected);
        await session.SaveChangesAsync();

        var client = Substitute.For<IBrokerAdeptClient>();
        client.NormalizeAsync(rawPayloadRef, Arg.Any<CancellationToken>())
            .Returns(new AdeptNormalizationResult(
                true, "Property", "Bermuda", 5_000_000m, "Standard terms", "Acme Holdings LLC",
                DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), null));

        await NormalizeSubmissionViaAdeptHandler.Handle(
            corrected, session, client, NullLogger<NormalizeSubmissionViaAdeptHandler>.Instance, CancellationToken.None);

        var submission = await session.Events.AggregateStreamAsync<Submission>(submissionId);
        submission.ShouldNotBeNull();
        submission.NormalizationStatus.ShouldBe("Normalized");
        submission.ClassOfBusiness.ShouldBe("Property");
        await client.Received(1).NormalizeAsync(rawPayloadRef, Arg.Any<CancellationToken>());
    }

    // (3) resubmittedForNormalization == false -> no re-normalization (guard).
    [Fact]
    public async Task Resubmitted_for_normalization_false_does_not_re_trigger_normalization()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var rawPayloadRef = $"raw-payload-{submissionId:N}";
        session.Events.StartStream<Submission>(submissionId, ReceivedEvent(submissionId, rawPayloadRef));
        session.Events.Append(submissionId, new SubmissionNormalizationFailed(
            submissionId, "MissingClassCode", rawPayloadRef, DateTimeOffset.UtcNow));
        await session.SaveChangesAsync();

        var corrected = new SubmissionManuallyCorrected(
            submissionId, "ops-jane", "Noted the issue, not resubmitting yet", false, DateTimeOffset.UtcNow);
        session.Events.Append(submissionId, corrected);
        await session.SaveChangesAsync();

        var client = Substitute.For<IBrokerAdeptClient>();

        await NormalizeSubmissionViaAdeptHandler.Handle(
            corrected, session, client, NullLogger<NormalizeSubmissionViaAdeptHandler>.Instance, CancellationToken.None);

        var submission = await session.Events.AggregateStreamAsync<Submission>(submissionId);
        submission.ShouldNotBeNull();
        submission.NormalizationStatus.ShouldBe("Failed");
        await client.DidNotReceive().NormalizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
