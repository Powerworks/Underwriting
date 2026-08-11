using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Shouldly;
using Xunit;
using SubmissionExceptionQueueDoc = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionExceptionQueue.SubmissionExceptionQueue;
using SubmissionExceptionQueueProjector = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.SubmissionExceptionQueue.SubmissionExceptionQueueProjector;

namespace SubmissionIntake.IntegrationTests;

// 4.9 -- Layer 3 (Testcontainers-backed Postgres, real IDocumentSession.Store/Load)
// per design.md Test Strategy Layer 3, same fixture as 4.7's SubmissionQueueProjectorTests
// (shared collection).
[Collection(SubmissionIntakePostgresCollection.Name)]
public class SubmissionExceptionQueueProjectorTests(SubmissionIntakePostgresFixture fixture)
{
    private static SubmissionNormalizationFailed FailedEvent(Guid submissionId, DateTimeOffset attemptedAt) => new(
        submissionId,
        "Unrecognized class code",
        "raw-payload-ref-456",
        attemptedAt);

    private static BrokerSubmissionReceived ReceivedEvent(Guid submissionId) => new(
        submissionId,
        "Acme Brokerage LLC",
        "jane.doe@acmebrokerage.com",
        "raw-payload-ref-456",
        "Email",
        DateTimeOffset.UtcNow.AddMinutes(-10));

    // Submitting SubmissionNormalizationFailed alone produces a SubmissionExceptionQueue
    // row with failureReason/attemptedAt and status set to the "Failed" literal
    // (mirrors Submission.Apply(SubmissionNormalizationFailed)'s write-side literal).
    [Fact]
    public async Task SubmissionNormalizationFailed_produces_SubmissionExceptionQueue_row_with_failureReason_and_status()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var attemptedAt = DateTimeOffset.UtcNow;
        var failed = FailedEvent(submissionId, attemptedAt);

        await SubmissionExceptionQueueProjector.Handle(failed, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var queueItem = await querySession.LoadAsync<SubmissionExceptionQueueDoc>(submissionId);

        queueItem.ShouldNotBeNull();
        queueItem.SubmissionId.ShouldBe(submissionId);
        queueItem.FailureReason.ShouldBe("Unrecognized class code");
        queueItem.AttemptedAt.ShouldBe(attemptedAt);
        queueItem.Status.ShouldBe("Failed");
    }

    // Task 4.9 field-tracing: SubmissionNormalizationFailed carries no brokerFirmId,
    // so BrokerSubmissionReceived creates the row with BrokerFirmId;
    // SubmissionNormalizationFailed then upserts onto that same row (not
    // insert-only) without clobbering it -- same shape as SubmissionQueueProjector's
    // 4.7.1 fix.
    [Fact]
    public async Task BrokerSubmissionReceived_and_SubmissionNormalizationFailed_populate_BrokerFirmId()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var attemptedAt = DateTimeOffset.UtcNow;
        var received = ReceivedEvent(submissionId);
        var failed = FailedEvent(submissionId, attemptedAt);

        await SubmissionExceptionQueueProjector.Handle(received, session, CancellationToken.None);
        await SubmissionExceptionQueueProjector.Handle(failed, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var queueItem = await querySession.LoadAsync<SubmissionExceptionQueueDoc>(submissionId);

        queueItem.ShouldNotBeNull();
        queueItem.SubmissionId.ShouldBe(submissionId);
        queueItem.BrokerFirmId.ShouldBe("Acme Brokerage LLC");
        queueItem.FailureReason.ShouldBe("Unrecognized class code");
        queueItem.AttemptedAt.ShouldBe(attemptedAt);
        queueItem.Status.ShouldBe("Failed");
    }

    // 8.5 -- design.md Technical Decisions gap-fix: "status field otherwise has no
    // event that can ever move it off its initial value" -- SubmissionManuallyCorrected
    // moves a Failed exception-queue entry's status to "Corrected" (DEC-008: entries
    // persist indefinitely until a correction).
    [Fact]
    public async Task SubmissionManuallyCorrected_updates_status_to_Corrected()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var attemptedAt = DateTimeOffset.UtcNow;
        var failed = FailedEvent(submissionId, attemptedAt);
        var corrected = new SubmissionManuallyCorrected(
            submissionId, "ops-jane", "Fixed missing class code", true, DateTimeOffset.UtcNow);

        await SubmissionExceptionQueueProjector.Handle(failed, session, CancellationToken.None);
        await SubmissionExceptionQueueProjector.Handle(corrected, session, CancellationToken.None);

        await using var querySession = fixture.Store.LightweightSession();
        var queueItem = await querySession.LoadAsync<SubmissionExceptionQueueDoc>(submissionId);

        queueItem.ShouldNotBeNull();
        queueItem.Status.ShouldBe("Corrected");
        // Prior fields untouched by the status-only update.
        queueItem.FailureReason.ShouldBe("Unrecognized class code");
        queueItem.AttemptedAt.ShouldBe(attemptedAt);
    }
}
