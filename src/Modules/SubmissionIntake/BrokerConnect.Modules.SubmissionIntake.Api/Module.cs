using BrokerConnect.BuildingBlocks.Domain;
using Marten;

namespace BrokerConnect.Modules.SubmissionIntake.Api;

public sealed class SubmissionIntakeModule : IMartenModuleConfiguration
{
    public string SchemaName => "submissionintake";

    public void Configure(StoreOptions options)
    {
        options.Events.DatabaseSchemaName = SchemaName;

        // Design.md Technical Decisions: Submission has no Inline snapshot —
        // nothing queries it by id directly (unlike 001's AuthorityLimit, which
        // Underwriting Decisioning needs read-your-own-write on). No projection
        // registration here yet; read models land per-phase as their own tasks.
    }

    // No IntegrationEventQueueName override yet — no confirmed inbound
    // integration event for this module at this phase.
}
