using BrokerConnect.Modules.SubmissionIntake.Api.Automations.DetectPotentialDuplicateOnNormalization;
using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using SubmissionQueueDoc = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionQueue.SubmissionQueue;

namespace SubmissionIntake.IntegrationTests;

// 5.3 -- Layer 3 (Testcontainers-backed Postgres, real AggregateStreamAsync/FetchStreamAsync
// + SubmissionQueue doc seeding), same shared fixture as 4.4/4.7 (SubmissionIntakePostgresFixture).
// per design.md Automations table: DetectPotentialDuplicateOnNormalization triggers on
// SubmissionNormalized (success only), exact-field match on classOfBusiness + territory +
// namedInsured against SubmissionQueue (DEC-010), appends PotentialDuplicateSubmissionDetected
// on the NEW submission's own stream if a match is found.
//
// RED: DetectPotentialDuplicateOnNormalizationHandler does not exist yet (created in 5.4).
// This file is expected to fail to compile until then -- that is the correct RED state.
[Collection(SubmissionIntakePostgresCollection.Name)]
public class DetectPotentialDuplicateOnNormalizationHandlerTests(SubmissionIntakePostgresFixture fixture)
{
    private static BrokerSubmissionReceived ReceivedEvent(Guid submissionId) => new(
        submissionId,
        "Acme Brokerage LLC",
        "jane.doe@acmebrokerage.com",
        "raw-payload-ref-123",
        "Email",
        DateTimeOffset.UtcNow);

    private static SubmissionNormalized NormalizedEvent(
        Guid submissionId,
        string classOfBusiness = "Property",
        string territory = "Bermuda",
        string namedInsured = "Acme Holdings LLC") => new(
        submissionId,
        classOfBusiness,
        territory,
        5_000_000m,
        "Standard terms",
        namedInsured,
        DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
        "Normalized",
        DateTimeOffset.UtcNow);

    private static SubmissionQueueDoc QueueEntry(
        Guid submissionId,
        string classOfBusiness = "Property",
        string territory = "Bermuda",
        string namedInsured = "Acme Holdings LLC") => new()
    {
        SubmissionId = submissionId,
        BrokerFirmId = "Acme Brokerage LLC",
        ClassOfBusiness = classOfBusiness,
        Territory = territory,
        NamedInsured = namedInsured,
        LineSizeSought = 5_000_000m,
        ReceivedAt = DateTimeOffset.UtcNow,
        Status = "Normalized",
    };

    // (1) exact-field match (classOfBusiness + territory + namedInsured, DEC-010) against
    // an existing open SubmissionQueue entry -> PotentialDuplicateSubmissionDetected appended
    // on the NEW submission's own stream.
    [Fact]
    public async Task Matching_SubmissionQueue_entry_appends_PotentialDuplicateSubmissionDetected()
    {
        await using var session = fixture.Store.LightweightSession();

        var originalSubmissionId = Guid.NewGuid();
        session.Store(QueueEntry(originalSubmissionId));
        await session.SaveChangesAsync();

        var newSubmissionId = Guid.NewGuid();
        var received = ReceivedEvent(newSubmissionId);
        session.Events.StartStream<Submission>(newSubmissionId, received);
        await session.SaveChangesAsync();

        var normalized = NormalizedEvent(newSubmissionId);

        await DetectPotentialDuplicateOnNormalizationHandler.Handle(
            normalized, session, NullLogger<DetectPotentialDuplicateOnNormalizationHandler>.Instance, CancellationToken.None);

        var newSubmission = await session.Events.AggregateStreamAsync<Submission>(newSubmissionId);
        newSubmission.ShouldNotBeNull();
        newSubmission.IsPossibleDuplicate.ShouldBeTrue();
        newSubmission.SuspectedOriginalSubmissionId.ShouldBe(originalSubmissionId);
    }

    // (2) no matching SubmissionQueue entry (classOfBusiness/territory/namedInsured all
    // differ) -> nothing appended on the new submission's stream.
    [Fact]
    public async Task No_matching_SubmissionQueue_entry_appends_nothing()
    {
        await using var session = fixture.Store.LightweightSession();

        var unrelatedSubmissionId = Guid.NewGuid();
        session.Store(QueueEntry(
            unrelatedSubmissionId, classOfBusiness: "Casualty", territory: "London", namedInsured: "Globex Corp"));
        await session.SaveChangesAsync();

        var newSubmissionId = Guid.NewGuid();
        var received = ReceivedEvent(newSubmissionId);
        session.Events.StartStream<Submission>(newSubmissionId, received);
        await session.SaveChangesAsync();

        var normalized = NormalizedEvent(newSubmissionId);

        await DetectPotentialDuplicateOnNormalizationHandler.Handle(
            normalized, session, NullLogger<DetectPotentialDuplicateOnNormalizationHandler>.Instance, CancellationToken.None);

        var events = await session.Events.FetchStreamAsync(newSubmissionId);
        events.ShouldNotContain(e => e.EventType == typeof(PotentialDuplicateSubmissionDetected));

        var newSubmission = await session.Events.AggregateStreamAsync<Submission>(newSubmissionId);
        newSubmission.ShouldNotBeNull();
        newSubmission.IsPossibleDuplicate.ShouldBeFalse();
    }

    // (3) idempotency: submission already flagged (IsPossibleDuplicate == true from a prior
    // PotentialDuplicateSubmissionDetected) is not re-flagged on redelivery of the same
    // trigger event, per Architecture Constraints / Automations table idempotency guard.
    [Fact]
    public async Task Already_flagged_submission_is_not_re_flagged()
    {
        await using var session = fixture.Store.LightweightSession();

        var originalSubmissionId = Guid.NewGuid();
        session.Store(QueueEntry(originalSubmissionId));
        await session.SaveChangesAsync();

        var newSubmissionId = Guid.NewGuid();
        var received = ReceivedEvent(newSubmissionId);
        var normalized = NormalizedEvent(newSubmissionId);
        var alreadyDetected = new PotentialDuplicateSubmissionDetected(
            newSubmissionId, originalSubmissionId, "classOfBusiness+territory+namedInsured", null, DateTimeOffset.UtcNow);
        session.Events.StartStream<Submission>(newSubmissionId, received, normalized, alreadyDetected);
        await session.SaveChangesAsync();

        var countBeforeRedelivery = (await session.Events.FetchStreamAsync(newSubmissionId)).Count;

        await DetectPotentialDuplicateOnNormalizationHandler.Handle(
            normalized, session, NullLogger<DetectPotentialDuplicateOnNormalizationHandler>.Instance, CancellationToken.None);

        var eventsAfterRedelivery = await session.Events.FetchStreamAsync(newSubmissionId);
        eventsAfterRedelivery.Count.ShouldBe(countBeforeRedelivery);
    }
}
