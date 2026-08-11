using BrokerConnect.Modules.SubmissionIntake.Api.Commands.ConfirmSubmissionDistinct;
using BrokerConnect.Modules.SubmissionIntake.Api.Commands.SupersedeSubmission;
using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace SubmissionIntake.IntegrationTests;

// 5.7 -- Layer 3 (Testcontainers-backed Postgres, real FetchForWriting/AggregateStreamAsync
// stream-targeting), same shared fixture as 4.4/4.7/5.3 (SubmissionIntakePostgresFixture).
// Per design.md Commands table:
//   SupersedeSubmission        -> POST .../{originalSubmissionId}/supersede
//                                  -> SubmissionSuperseded appended on the ORIGINAL's own stream.
//   ConfirmSubmissionDistinct  -> POST .../{submissionId}/confirm-distinct
//                                  -> SubmissionConfirmedDistinct appended on the FLAGGED/NEW's
//                                     own stream.
//
// RED: SupersedeSubmissionHandler / ConfirmSubmissionDistinctHandler do not exist yet (created
// in 5.8). This file is expected to fail to compile until then -- that is the correct RED state.
[Collection(SubmissionIntakePostgresCollection.Name)]
public class DuplicateResolutionCommandsTests(SubmissionIntakePostgresFixture fixture)
{
    private static BrokerSubmissionReceived ReceivedEvent(Guid submissionId, string brokerFirmId) => new(
        submissionId,
        brokerFirmId,
        "jane.doe@acmebrokerage.com",
        "raw-payload-ref-123",
        "Email",
        DateTimeOffset.UtcNow);

    // (1) SupersedeSubmission appends SubmissionSuperseded on the ORIGINAL's own stream, not
    // the superseding submission's stream (design.md Commands table).
    [Fact]
    public async Task SupersedeSubmission_appends_SubmissionSuperseded_on_the_originals_stream()
    {
        await using var session = fixture.Store.LightweightSession();

        var originalSubmissionId = Guid.NewGuid();
        var supersedingSubmissionId = Guid.NewGuid();
        // Unique-per-test literals (suffixed with each submission's own id) so this test's
        // seed rows can never collide with data left behind by other test classes in the
        // same shared-fixture Postgres collection (5.4.1 fix).
        var originalBrokerFirmId = $"Acme Brokerage LLC {originalSubmissionId:N}";
        var supersedingBrokerFirmId = $"Acme Brokerage LLC {supersedingSubmissionId:N}";

        session.Events.StartStream<Submission>(originalSubmissionId, ReceivedEvent(originalSubmissionId, originalBrokerFirmId));
        session.Events.StartStream<Submission>(supersedingSubmissionId, ReceivedEvent(supersedingSubmissionId, supersedingBrokerFirmId));
        await session.SaveChangesAsync();

        var request = new SupersedeSubmissionRequest(supersedingSubmissionId, $"jane.underwriter-{originalSubmissionId:N}");

        await SupersedeSubmissionHandler.Handle(
            originalSubmissionId, request, session, NullLogger<SupersedeSubmissionHandler>.Instance, CancellationToken.None);

        var originalEvents = await session.Events.FetchStreamAsync(originalSubmissionId);
        originalEvents.ShouldContain(e => e.EventType == typeof(SubmissionSuperseded));
        var superseded = (SubmissionSuperseded)originalEvents.Single(e => e.EventType == typeof(SubmissionSuperseded)).Data;
        superseded.OriginalSubmissionId.ShouldBe(originalSubmissionId);
        superseded.SupersedingSubmissionId.ShouldBe(supersedingSubmissionId);

        var supersedingEvents = await session.Events.FetchStreamAsync(supersedingSubmissionId);
        supersedingEvents.ShouldNotContain(e => e.EventType == typeof(SubmissionSuperseded));

        var original = await session.Events.AggregateStreamAsync<Submission>(originalSubmissionId);
        original.ShouldNotBeNull();
        original.SupersededBySubmissionId.ShouldBe(supersedingSubmissionId);
    }

    // (2) ConfirmSubmissionDistinct appends SubmissionConfirmedDistinct on the FLAGGED/NEW's
    // own stream, not the suspected original's stream (design.md Commands table).
    [Fact]
    public async Task ConfirmSubmissionDistinct_appends_SubmissionConfirmedDistinct_on_the_flagged_new_stream()
    {
        await using var session = fixture.Store.LightweightSession();

        var originalSubmissionId = Guid.NewGuid();
        var newSubmissionId = Guid.NewGuid();
        // Unique-per-test literals (5.4.1 fix).
        var originalBrokerFirmId = $"Acme Brokerage LLC {originalSubmissionId:N}";
        var newBrokerFirmId = $"Acme Brokerage LLC {newSubmissionId:N}";

        session.Events.StartStream<Submission>(originalSubmissionId, ReceivedEvent(originalSubmissionId, originalBrokerFirmId));

        var flagged = new PotentialDuplicateSubmissionDetected(
            newSubmissionId, originalSubmissionId, "classOfBusiness+territory+namedInsured", null, DateTimeOffset.UtcNow);
        session.Events.StartStream<Submission>(newSubmissionId, ReceivedEvent(newSubmissionId, newBrokerFirmId), flagged);
        await session.SaveChangesAsync();

        var request = new ConfirmSubmissionDistinctRequest($"jane.underwriter-{newSubmissionId:N}");

        await ConfirmSubmissionDistinctHandler.Handle(
            newSubmissionId, request, session, NullLogger<ConfirmSubmissionDistinctHandler>.Instance, CancellationToken.None);

        var newEvents = await session.Events.FetchStreamAsync(newSubmissionId);
        newEvents.ShouldContain(e => e.EventType == typeof(SubmissionConfirmedDistinct));
        var confirmed = (SubmissionConfirmedDistinct)newEvents.Single(e => e.EventType == typeof(SubmissionConfirmedDistinct)).Data;
        confirmed.SubmissionId.ShouldBe(newSubmissionId);
        confirmed.SuspectedOriginalSubmissionId.ShouldBe(originalSubmissionId);

        var originalEvents = await session.Events.FetchStreamAsync(originalSubmissionId);
        originalEvents.ShouldNotContain(e => e.EventType == typeof(SubmissionConfirmedDistinct));

        var newSubmission = await session.Events.AggregateStreamAsync<Submission>(newSubmissionId);
        newSubmission.ShouldNotBeNull();
        newSubmission.IsConfirmedDistinct.ShouldBeTrue();
    }
}
