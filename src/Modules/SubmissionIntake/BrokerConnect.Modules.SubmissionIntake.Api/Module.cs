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

    // ADR-004: versioned integration events over durable per-module queues, never a
    // direct cross-module call. This module is the first CONSUMER in this codebase
    // (001/AuthorityAdministration only ever publishes AuthorityLimitChangedV1) --
    // Program.cs's generic `if (module.IntegrationEventQueueName is { } queueName)
    // opts.ListenToRabbitQueue(queueName).UseDurableInbox()` loop picks this up with
    // no further wiring needed there. Queue name follows the same
    // "{module}.{integration-event}" convention documented for AuthorityLimitChangedV1's
    // publish side.
    public string? IntegrationEventQueueName => "submissionintake.submission-assessed-v1";
}
