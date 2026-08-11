using BrokerConnect.Modules.SubmissionIntake.Api;
using BrokerConnect.Modules.SubmissionIntake.Api.Automations.NormalizeSubmissionViaAdept;
using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using BrokerConnect.Modules.SubmissionIntake.Infrastructure;
using JasperFx;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace SubmissionIntake.IntegrationTests;

// 4.4 — Layer 3 (Testcontainers-backed Postgres, real AggregateStreamAsync/FetchForWriting)
// per design.md Test Strategy Layer 3: IBrokerAdeptClient mocked (NSubstitute) at the
// boundary, the Marten interaction is what needs the real store, not the external HTTP
// call. Self-contained: fixture lives in this file since it's the only file this task
// (4.4) is scoped to touch.
//
// RED: NormalizeSubmissionViaAdeptHandler does not exist yet (created in 4.5). This file
// is expected to fail to compile until then — that is the correct RED state.
public sealed class SubmissionIntakePostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public IDocumentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var module = new SubmissionIntakeModule();
        Store = DocumentStore.For(opts =>
        {
            opts.Connection(_container.GetConnectionString());
            module.Configure(opts);
            opts.AutoCreateSchemaObjects = AutoCreate.All;
        });
    }

    public async Task DisposeAsync()
    {
        Store.Dispose();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class SubmissionIntakePostgresCollection : ICollectionFixture<SubmissionIntakePostgresFixture>
{
    public const string Name = "SubmissionIntakePostgres";
}

[Collection(SubmissionIntakePostgresCollection.Name)]
public class NormalizeSubmissionViaAdeptHandlerTests(SubmissionIntakePostgresFixture fixture)
{
    private static BrokerSubmissionReceived ReceivedEvent(Guid submissionId, string rawPayloadRef = "{\"foo\":\"bar\"}") =>
        new(submissionId, "acme-brokers", "jane@acme.com", rawPayloadRef, "api", DateTimeOffset.UtcNow);

    // (1) success -> SubmissionNormalized appended
    [Fact]
    public async Task Successful_adept_normalization_appends_SubmissionNormalized()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var received = ReceivedEvent(submissionId);
        session.Events.StartStream<Submission>(submissionId, received);
        await session.SaveChangesAsync();

        var client = Substitute.For<IBrokerAdeptClient>();
        client.NormalizeAsync(received.RawPayloadRef, Arg.Any<CancellationToken>())
            .Returns(new AdeptNormalizationResult(
                true, "Property", "Bermuda", 5_000_000m, "Standard terms", "Acme Holdings LLC",
                DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), null));

        await NormalizeSubmissionViaAdeptHandler.Handle(
            received, session, client, NullLogger<NormalizeSubmissionViaAdeptHandler>.Instance, CancellationToken.None);

        var submission = await session.Events.AggregateStreamAsync<Submission>(submissionId);
        submission.ShouldNotBeNull();
        submission.ClassOfBusiness.ShouldBe("Property");
        submission.Territory.ShouldBe("Bermuda");
        submission.NamedInsured.ShouldBe("Acme Holdings LLC");
        submission.NormalizationStatus.ShouldNotBeNull();
        submission.NormalizationStatus.ShouldNotBe("Failed");
    }

    // (2) AdeptNormalizationResult.Succeeded == false -> SubmissionNormalizationFailed appended, raw payload preserved
    [Fact]
    public async Task Failed_adept_normalization_appends_SubmissionNormalizationFailed_and_preserves_raw_payload()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var received = ReceivedEvent(submissionId);
        session.Events.StartStream<Submission>(submissionId, received);
        await session.SaveChangesAsync();

        var client = Substitute.For<IBrokerAdeptClient>();
        client.NormalizeAsync(received.RawPayloadRef, Arg.Any<CancellationToken>())
            .Returns(new AdeptNormalizationResult(
                false, null, null, null, null, null, null, "UnrecognizedClassCode"));

        await NormalizeSubmissionViaAdeptHandler.Handle(
            received, session, client, NullLogger<NormalizeSubmissionViaAdeptHandler>.Instance, CancellationToken.None);

        var submission = await session.Events.AggregateStreamAsync<Submission>(submissionId);
        submission.ShouldNotBeNull();
        submission.NormalizationStatus.ShouldBe("Failed");
        // AC-4.1: raw payload never discarded on a failed normalization.
        submission.RawPayloadRef.ShouldBe(received.RawPayloadRef);
    }

    // (3) redelivered BrokerSubmissionReceived when NormalizationStatus already set -> no duplicate append
    [Fact]
    public async Task Redelivered_BrokerSubmissionReceived_after_normalization_already_set_does_not_duplicate_append()
    {
        await using var session = fixture.Store.LightweightSession();
        var submissionId = Guid.NewGuid();
        var received = ReceivedEvent(submissionId);
        session.Events.StartStream<Submission>(submissionId, received);
        await session.SaveChangesAsync();

        var client = Substitute.For<IBrokerAdeptClient>();
        client.NormalizeAsync(received.RawPayloadRef, Arg.Any<CancellationToken>())
            .Returns(new AdeptNormalizationResult(
                true, "Property", "Bermuda", 5_000_000m, "Standard terms", "Acme Holdings LLC",
                DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), null));

        // First delivery: normalizes as usual.
        await NormalizeSubmissionViaAdeptHandler.Handle(
            received, session, client, NullLogger<NormalizeSubmissionViaAdeptHandler>.Instance, CancellationToken.None);

        var events = await session.Events.FetchStreamAsync(submissionId);
        var countAfterFirstDelivery = events.Count;

        // Redelivery of the same trigger event (Architecture Constraints idempotency guard).
        await NormalizeSubmissionViaAdeptHandler.Handle(
            received, session, client, NullLogger<NormalizeSubmissionViaAdeptHandler>.Instance, CancellationToken.None);

        var eventsAfterRedelivery = await session.Events.FetchStreamAsync(submissionId);
        eventsAfterRedelivery.Count.ShouldBe(countAfterFirstDelivery);
        await client.Received(1).NormalizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
