using BrokerConnect.Modules.AuthorityAdministration.Api;
using JasperFx;
using Marten;
using Testcontainers.PostgreSql;
using Xunit;

namespace AuthorityAdministration.IntegrationTests;

/// <summary>
/// One Postgres container per xUnit collection, per build-state-change/SKILL.md's
/// documented Layer 3 pattern — the same module Marten config production uses,
/// with AutoCreate.All so the schema exists without a separate migration step.
/// </summary>
public sealed class AuthorityAdministrationPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

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
    }

    public async Task DisposeAsync()
    {
        Store.Dispose();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class AuthorityAdministrationPostgresCollection : ICollectionFixture<AuthorityAdministrationPostgresFixture>
{
    public const string Name = "AuthorityAdministrationPostgres";
}
