namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents;

// A runner's poll passes on every event waiting in the shared outbox, which in the test database are the events of every other
// test, so the tests that publish run alone, after the collections that run in parallel.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationEventPublishers
{
    public const string Name = "Integration events";
}
