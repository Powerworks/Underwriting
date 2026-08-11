using BrokerConnect.Modules.SubmissionIntake.Api.Automations.RouteSubmissionOnReceipt;
using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using BrokerConnect.Modules.SubmissionIntake.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace SubmissionIntake.IntegrationTests;

// 6.4 -- Layer 3 (Testcontainers-backed Postgres, real AggregateStreamAsync/FetchStreamAsync),
// same shared fixture as 4.4/4.7/5.3 (SubmissionIntakePostgresFixture). Per design.md
// Automations table: RouteSubmissionOnReceipt triggers on BrokerSubmissionReceived, calls
// IBrokerPanelAuthorizationSource.IsAuthorizedAsync (mocked here via NSubstitute -- the
// Marten interaction is what needs the real store, not the authorization boundary), and
// appends SubmissionRoutingRejected with requestedCellId/requestedClassOfBusiness mirroring
// BrokerSubmissionReceived's CellIdHint/ClassOfBusinessHint (6.3.1) if rejected; DEC-009 is
// a hard block, so authorized is a no-op (no broker feedback either way).
//
// RED: RouteSubmissionOnReceiptHandler does not exist yet (created in 6.5). This file is
// expected to fail to compile until then -- that is the correct RED state.
[Collection(SubmissionIntakePostgresCollection.Name)]
public class RouteSubmissionOnReceiptHandlerTests(SubmissionIntakePostgresFixture fixture)
{
    // Unique-per-test literals (GUID-suffixed, per the 5.4.1 convention) so this test's
    // seed data can never collide with rows left behind by other test classes in the
    // same shared-fixture Postgres collection.
    private static BrokerSubmissionReceived ReceivedEvent(Guid submissionId) => new(
        submissionId,
        $"Acme Brokerage LLC {submissionId:N}",
        "jane.doe@acmebrokerage.com",
        "raw-payload-ref-123",
        "Email",
        DateTimeOffset.UtcNow,
        CellIdHint: $"cell-{submissionId:N}",
        ClassOfBusinessHint: $"Property-{submissionId:N}");

    // (1) authorized broker panel -> no-op, no SubmissionRoutingRejected appended.
    [Fact]
    public async Task Authorized_broker_panel_results_in_no_op()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var received = ReceivedEvent(submissionId);
        session.Events.StartStream<Submission>(submissionId, received);
        await session.SaveChangesAsync();

        var authorizationSource = Substitute.For<IBrokerPanelAuthorizationSource>();
        authorizationSource.IsAuthorizedAsync(
                received.BrokerFirmId, received.CellIdHint, received.ClassOfBusinessHint, Arg.Any<CancellationToken>())
            .Returns(true);

        await RouteSubmissionOnReceiptHandler.Handle(
            received, session, authorizationSource, NullLogger<RouteSubmissionOnReceiptHandler>.Instance, CancellationToken.None);

        var events = await session.Events.FetchStreamAsync(submissionId);
        events.ShouldNotContain(e => e.EventType == typeof(SubmissionRoutingRejected));

        var submission = await session.Events.AggregateStreamAsync<Submission>(submissionId);
        submission.ShouldNotBeNull();
        submission.IsRoutingRejected.ShouldBeFalse();
    }

    // (2) rejected broker panel -> SubmissionRoutingRejected appended, requestedCellId/
    // requestedClassOfBusiness mirroring the command's hint fields (BrokerSubmissionReceived.
    // CellIdHint/ClassOfBusinessHint, per 6.3.1).
    [Fact]
    public async Task Unauthorized_broker_panel_appends_SubmissionRoutingRejected_mirroring_hints()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var received = ReceivedEvent(submissionId);
        session.Events.StartStream<Submission>(submissionId, received);
        await session.SaveChangesAsync();

        var authorizationSource = Substitute.For<IBrokerPanelAuthorizationSource>();
        authorizationSource.IsAuthorizedAsync(
                received.BrokerFirmId, received.CellIdHint, received.ClassOfBusinessHint, Arg.Any<CancellationToken>())
            .Returns(false);

        await RouteSubmissionOnReceiptHandler.Handle(
            received, session, authorizationSource, NullLogger<RouteSubmissionOnReceiptHandler>.Instance, CancellationToken.None);

        var events = await session.Events.FetchStreamAsync(submissionId);
        var rejected = events.SingleOrDefault(e => e.EventType == typeof(SubmissionRoutingRejected))?.Data as SubmissionRoutingRejected;
        rejected.ShouldNotBeNull();
        rejected.RequestedCellId.ShouldBe(received.CellIdHint);
        rejected.RequestedClassOfBusiness.ShouldBe(received.ClassOfBusinessHint);

        var submission = await session.Events.AggregateStreamAsync<Submission>(submissionId);
        submission.ShouldNotBeNull();
        submission.IsRoutingRejected.ShouldBeTrue();
    }
}
