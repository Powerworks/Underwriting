using BrokerConnect.Modules.AuthorityAdministration.Api;
using JasperFx;
using JasperFx.Events.Daemon;
using Marten;
using Testcontainers.PostgreSql;
using Xunit;

namespace AuthorityAdministration.IntegrationTests;

/// <summary>
/// One Postgres container per xUnit collection, per build-state-change/SKILL.md's
/// documented Layer 3 pattern — the same module Marten config production uses,
/// with AutoCreate.All so the schema exists without a separate migration step.
/// Also runs the async projection daemon (Solo mode, same as Program.cs) — without
/// it, Async-lifecycle projections like CellAuthorityRegister (T050) never process
/// events; Inline snapshots (AuthorityMatrix) are unaffected either way.
/// </summary>
public sealed class AuthorityAdministrationPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private IProjectionDaemon _daemon = null!;

    public IDocumentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var module = new AuthorityAdministrationModule();
        Store = DocumentStore.For(opts =>
        {
            opts.Connection(_container.GetConnectionString());
            module.Configure(opts);
            opts.AutoCreateSchemaObjects = AutoCreate.All;
        });

        _daemon = await Store.BuildProjectionDaemonAsync();
        await _daemon.StartAllAsync();

        // Marten's source generator prints a build-time "The async daemon is
        // disabled" warning for this project regardless — it only recognizes the
        // declarative AddAsyncDaemon(...) DI call (used in Program.cs) as "enabled,"
        // not this manual BuildProjectionDaemonAsync/StartAllAsync pair. Confirmed
        // benign: CellAuthorityRegisterProjectionTests asserts on real
        // daemon-produced documents and passes.
    }

    /// <summary>
    /// Blocks until the async daemon has caught up to every event committed so far —
    /// call after appending events an Async-lifecycle projection reacts to, before
    /// asserting on its projected document (it will not exist/be current otherwise).
    /// </summary>
    public Task WaitForProjectionsAsync(TimeSpan? timeout = null) =>
        _daemon.WaitForNonStaleData(timeout ?? TimeSpan.FromSeconds(30));

    public async Task DisposeAsync()
    {
        await _daemon.StopAllAsync();
        _daemon.Dispose();
        Store.Dispose();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class AuthorityAdministrationPostgresCollection : ICollectionFixture<AuthorityAdministrationPostgresFixture>
{
    public const string Name = "AuthorityAdministrationPostgres";
}
