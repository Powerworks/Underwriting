using BrokerConnect.BuildingBlocks.Domain;
using BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.CellAuthorityRegister;
using BrokerConnect.Modules.AuthorityAdministration.Api.ReadModels.UnderwriterAuthorityRegister;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;
using JasperFx.Events.Projections;
using Marten;

namespace BrokerConnect.Modules.AuthorityAdministration.Api;

public sealed class AuthorityAdministrationModule : IMartenModuleConfiguration
{
    public string SchemaName => "authority";

    public void Configure(StoreOptions options)
    {
        options.Events.DatabaseSchemaName = SchemaName;

        // Constitution Architecture Constraints: Inline only because AuthorityLimit
        // (queried as AuthorityMatrix, tasks.md US2/T046) is read by id at decision
        // time — Underwriting Decisioning's AssessSubmission needs read-your-own-write
        // in the same request (Clarified 2026-08-09 / SC-001). The P1 handlers
        // (US1/US3) already depend on session.Query<AuthorityLimit>() working, so
        // this is registered now rather than deferred to a later phase — a query
        // against an unregistered snapshot type silently returns nothing, which
        // would make the duplicate/cascade checks pass when they should reject.
        options.Projections.Snapshot<AuthorityLimit>(SnapshotLifecycle.Inline);

        // T050/US6 (plan.md Architecture Constraints check): plain async projection —
        // no same-request read-after-write requirement, unlike AuthorityMatrix above.
        // Requires the async daemon to actually run (Program.cs's AddAsyncDaemon /
        // the test fixture's BuildProjectionDaemonAsync) — registering it here alone
        // is not enough for documents to ever get produced.
        options.Projections.Add<CellAuthorityRegisterProjector>(ProjectionLifecycle.Async);

        // T053/US8 — same async-projection requirements as CellAuthorityRegister above.
        options.Projections.Add<UnderwriterAuthorityRegisterProjector>(ProjectionLifecycle.Async);
    }

    // No IntegrationEventQueueName override — this module only publishes
    // AuthorityLimitChangedV1 (research.md Decision 4), it never consumes another
    // module's integration events, so it needs no inbound durable queue.
}
