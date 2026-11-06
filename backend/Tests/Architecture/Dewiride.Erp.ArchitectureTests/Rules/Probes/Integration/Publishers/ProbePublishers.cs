using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Publishing;

namespace Dewiride.Erp.ArchitectureTests.Rules.Probes.Integration.Publishers;

// A publisher placed as a module's Integration/Publishers folder places it.
public static class ProbePublishers
{
    public sealed class LedgerPostedPublisher(IIntegrationEventPublisher publisher)
    {
        public IIntegrationEventPublisher Publisher { get; } = publisher;
    }
}
