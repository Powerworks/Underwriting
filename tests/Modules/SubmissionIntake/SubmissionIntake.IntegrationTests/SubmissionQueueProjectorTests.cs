using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Shouldly;
using Xunit;
using SubmissionQueueDoc = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionQueue.SubmissionQueue;
using SubmissionQueueProjector = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionQueue.SubmissionQueueProjector;

namespace SubmissionIntake.IntegrationTests;

// 4.7 -- Layer 3 (Testcontainers-backed Postgres, real IDocumentSession.Store/Load)
// per design.md Test Strategy Layer 3, same fixture as 4.4's
// NormalizeSubmissionViaAdeptHandlerTests (shared collection).
[Collection(SubmissionIntakePostgresCollection.Name)]
public class SubmissionQueueProjectorTests(SubmissionIntakePostgresFixture fixture)
{
    // classOfBusiness/territory/namedInsured are suffixed with the submission's own id
    // so the real SubmissionQueue rows this test persists (via the actual projector, not
    // just a hand-seeded doc) can never collide with DetectPotentialDuplicateOnNormalization's
    // exact-match tests, which share this fixture's Postgres collection (5.4.1 fix).
    private static SubmissionNormalized NormalizedEvent(Guid submissionId) => new(
        submissionId,
        $"Property-{submissionId:N}",
        $"Bermuda-{submissionId:N}",
        5_000_000m,
        "Standard terms",
        $"Acme Holdings LLC {submissionId:N}",
        DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
        "Normalized",
        DateTimeOffset.UtcNow);

    private static BrokerSubmissionReceived ReceivedEvent(Guid submissionId, DateTimeOffset receivedAt) => new(
        submissionId,
        "Acme Brokerage LLC",
        "jane.doe@acmebrokerage.com",
        "raw-payload-ref-123",
        "Email",
        receivedAt);

    // Submitting SubmissionNormalized produces a SubmissionQueue row with
    // namedInsured (Technical Decisions gap-fix) and status sourced from
    // normalizationStatus.
    [Fact]
    public async Task SubmissionNormalized_produces_SubmissionQueue_row_with_namedInsured_and_status()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var normalized = NormalizedEvent(submissionId);

        await SubmissionQueueProjector.Handle(normalized, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var queueItem = await querySession.LoadAsync<SubmissionQueueDoc>(submissionId);

        queueItem.ShouldNotBeNull();
        queueItem.SubmissionId.ShouldBe(submissionId);
        queueItem.NamedInsured.ShouldBe($"Acme Holdings LLC {submissionId:N}");
        queueItem.Status.ShouldBe("Normalized");
        queueItem.ClassOfBusiness.ShouldBe($"Property-{submissionId:N}");
        queueItem.Territory.ShouldBe($"Bermuda-{submissionId:N}");
        queueItem.LineSizeSought.ShouldBe(5_000_000m);
        // Task 4.7 scope: duplicate-detection fields aren't wired until 5.5 -- stay
        // at their unflagged defaults on a row created only from SubmissionNormalized.
        queueItem.IsPossibleDuplicate.ShouldBeFalse();
        queueItem.SuspectedOriginalSubmissionId.ShouldBeNull();
    }

    // 4.7.1 [FIX 4.7] -- BrokerSubmissionReceived creates the row with BrokerFirmId
    // and the real ReceivedAt; SubmissionNormalized then upserts onto that same row
    // (not insert-only) without clobbering those two fields.
    [Fact]
    public async Task BrokerSubmissionReceived_and_SubmissionNormalized_populate_BrokerFirmId_and_ReceivedAt()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var receivedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var received = ReceivedEvent(submissionId, receivedAt);
        var normalized = NormalizedEvent(submissionId);

        await SubmissionQueueProjector.Handle(received, session, CancellationToken.None);
        await SubmissionQueueProjector.Handle(normalized, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var queueItem = await querySession.LoadAsync<SubmissionQueueDoc>(submissionId);

        queueItem.ShouldNotBeNull();
        queueItem.SubmissionId.ShouldBe(submissionId);
        queueItem.BrokerFirmId.ShouldBe("Acme Brokerage LLC");
        queueItem.ReceivedAt.ShouldBe(receivedAt);
        queueItem.NamedInsured.ShouldBe($"Acme Holdings LLC {submissionId:N}");
        queueItem.Status.ShouldBe("Normalized");
    }

    // 5.5 gap-fix -- PotentialDuplicateSubmissionDetected flips IsPossibleDuplicate/
    // SuspectedOriginalSubmissionId, which otherwise have no event source at all
    // (design.md Technical Decisions).
    [Fact]
    public async Task PotentialDuplicateSubmissionDetected_flags_row_as_possible_duplicate()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var suspectedOriginalId = Guid.NewGuid();
        var normalized = NormalizedEvent(submissionId);
        var detected = new PotentialDuplicateSubmissionDetected(
            submissionId, suspectedOriginalId, "ExactFieldMatch", 1.0m, DateTimeOffset.UtcNow);

        await SubmissionQueueProjector.Handle(normalized, session, CancellationToken.None);
        await SubmissionQueueProjector.Handle(detected, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var queueItem = await querySession.LoadAsync<SubmissionQueueDoc>(submissionId);

        queueItem.ShouldNotBeNull();
        queueItem.IsPossibleDuplicate.ShouldBeTrue();
        queueItem.SuspectedOriginalSubmissionId.ShouldBe(suspectedOriginalId);
    }

    // 5.5 gap-fix -- SubmissionSuperseded appends to the ORIGINAL's own stream
    // (OriginalSubmissionId) and confirms it's a stale resubmission of the newer
    // submission, so its queue row is removed (delete-if-exists), mirroring the
    // SubmissionRoutingRejected retraction pattern.
    [Fact]
    public async Task SubmissionSuperseded_removes_original_row_from_queue()
    {
        await using var session = fixture.Store.LightweightSession();
        var originalSubmissionId = Guid.NewGuid();
        var supersedingSubmissionId = Guid.NewGuid();
        var normalized = NormalizedEvent(originalSubmissionId);
        var superseded = new SubmissionSuperseded(
            originalSubmissionId, supersedingSubmissionId, "jane.underwriter", DateTimeOffset.UtcNow);

        await SubmissionQueueProjector.Handle(normalized, session, CancellationToken.None);
        await SubmissionQueueProjector.Handle(superseded, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var queueItem = await querySession.LoadAsync<SubmissionQueueDoc>(originalSubmissionId);

        queueItem.ShouldBeNull();
    }

    // 5.5 gap-fix -- SubmissionConfirmedDistinct appends to the flagged/new
    // submission's own stream and clears the possible-duplicate flag ("both proceed
    // independently"), leaving the row active in the queue.
    [Fact]
    public async Task SubmissionConfirmedDistinct_clears_possible_duplicate_flag()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var suspectedOriginalId = Guid.NewGuid();
        var normalized = NormalizedEvent(submissionId);
        var detected = new PotentialDuplicateSubmissionDetected(
            submissionId, suspectedOriginalId, "ExactFieldMatch", 1.0m, DateTimeOffset.UtcNow);
        var confirmedDistinct = new SubmissionConfirmedDistinct(
            submissionId, suspectedOriginalId, "jane.underwriter", DateTimeOffset.UtcNow);

        await SubmissionQueueProjector.Handle(normalized, session, CancellationToken.None);
        await SubmissionQueueProjector.Handle(detected, session, CancellationToken.None);
        await SubmissionQueueProjector.Handle(confirmedDistinct, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var queueItem = await querySession.LoadAsync<SubmissionQueueDoc>(submissionId);

        queueItem.ShouldNotBeNull();
        queueItem.IsPossibleDuplicate.ShouldBeFalse();
        queueItem.SuspectedOriginalSubmissionId.ShouldBeNull();
    }

    // 6.8 -- design.md Edge Cases: a late SubmissionRoutingRejected (routing and
    // normalization run in parallel off the same trigger) must retract the row so the
    // submission "never appears in any underwriter's queue" (requirements.md US-2).
    [Fact]
    public async Task SubmissionRoutingRejected_removes_row_from_queue()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var normalized = NormalizedEvent(submissionId);
        var rejected = new SubmissionRoutingRejected(
            submissionId, "Acme Brokerage LLC", "cell-01", "Property", "PanelDoesNotCoverCell", DateTimeOffset.UtcNow);

        await SubmissionQueueProjector.Handle(normalized, session, CancellationToken.None);
        await SubmissionQueueProjector.Handle(rejected, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var queueItem = await querySession.LoadAsync<SubmissionQueueDoc>(submissionId);

        queueItem.ShouldBeNull();
    }
}
