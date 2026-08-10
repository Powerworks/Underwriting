using Marten;

namespace BrokerConnect.BuildingBlocks.Domain;

public interface IMartenModuleConfiguration
{
    string SchemaName { get; }
    void Configure(StoreOptions options);
    string? IntegrationEventQueueName => null; // default-implemented — most modules never override this
}
