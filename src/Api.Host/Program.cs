using BrokerConnect.BuildingBlocks.Domain;
using BrokerConnect.Modules.AuthorityAdministration.Api;
using JasperFx;
using JasperFx.Events.Daemon;
using Marten;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// One entry per module — Api.Host composes all modules into a single deployable
// (ADR-001). New modules register themselves here as their own feature lands.
IMartenModuleConfiguration[] modules = [new AuthorityAdministrationModule()];

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Database=brokerconnect;Username=postgres;Password=postgres";
var rabbitConnectionString = builder.Configuration.GetConnectionString("RabbitMQ")
    ?? "amqp://guest:guest@localhost:5672";

builder.Host.UseWolverine(opts =>
{
    foreach (var module in modules)
        opts.Discovery.IncludeAssembly(module.GetType().Assembly);

    opts.UseRabbitMq(new Uri(rabbitConnectionString)).AutoProvision();

    foreach (var module in modules)
        if (module.IntegrationEventQueueName is { } queueName)
            opts.ListenToRabbitQueue(queueName).UseDurableInbox();
});

builder.Services.AddMarten(opts =>
{
    opts.Connection(postgresConnectionString);
    foreach (var module in modules) module.Configure(opts);
})
    .IntegrateWithWolverine()
    // Required for any Async-lifecycle projection (e.g. CellAuthorityRegister,
    // T050) to ever actually run — registering a projection with
    // ProjectionLifecycle.Async in a module's Configure() only declares it; without
    // the daemon nothing processes events into documents. Solo mode: single
    // instance, no distributed leader election needed at this stage.
    .AddAsyncDaemon(DaemonMode.Solo);

builder.Services.AddWolverineHttp();

var app = builder.Build();

// Constitution Architecture Constraints: concurrency conflicts map to 409 centrally,
// once — not per-handler. JasperFx.ConcurrencyException (event-stream) and
// Marten.Exceptions.ConcurrentUpdateException (document-level) are two separate
// hierarchies, both handled here.
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception ex) when (ex is JasperFx.ConcurrencyException or Marten.Exceptions.ConcurrentUpdateException)
    {
        context.Response.Clear();
        await Results.Conflict("This resource was modified by someone else since you last loaded it. Reload and try again.")
            .ExecuteAsync(context);
    }
});

// Constitution Principle X: a health check endpoint, separate from business
// endpoints, exists before the first deploy to any shared environment.
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapWolverineEndpoints(opts => opts.UseDataAnnotationsValidationProblemDetailMiddleware());

await app.RunJasperFxCommands(args);
