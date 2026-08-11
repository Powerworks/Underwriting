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
    private static SubmissionNormalized NormalizedEvent(Guid submissionId) => new(
        submissionId,
        "Property",
        "Bermuda",
        5_000_000m,
        "Standard terms",
        "Acme Holdings LLC",
        DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
        "Normalized",
        DateTimeOffset.UtcNow);

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
        queueItem.NamedInsured.ShouldBe("Acme Holdings LLC");
        queueItem.Status.ShouldBe("Normalized");
        queueItem.ClassOfBusiness.ShouldBe("Property");
        queueItem.Territory.ShouldBe("Bermuda");
        queueItem.LineSizeSought.ShouldBe(5_000_000m);
        // Task 4.7 scope: duplicate-detection fields aren't wired until 5.5 -- stay
        // at their unflagged defaults on a row created only from SubmissionNormalized.
        queueItem.IsPossibleDuplicate.ShouldBeFalse();
        queueItem.SuspectedOriginalSubmissionId.ShouldBeNull();
    }
}
