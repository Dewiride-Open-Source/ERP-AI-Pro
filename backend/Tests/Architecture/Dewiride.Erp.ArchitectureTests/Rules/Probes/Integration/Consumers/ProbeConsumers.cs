using Dewiride.Erp.ArchitectureTests.Rules.Probes.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.ArchitectureTests.Rules.Probes.Integration.Consumers;

// Subscribers placed as a module's Integration/Consumers folder places them, one keeping the rule and one breaking it.
public static class ProbeConsumers
{
    public sealed class LedgerPostedRecorder : IIntegrationEventHandler<ProbeEvents.LedgerPosted>
    {
        public Task<Result> HandleAsync(ProbeEvents.LedgerPosted integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }

    public class LedgerPostedAuditor : IIntegrationEventHandler<ProbeEvents.LedgerPosted>
    {
        public Task<Result> HandleAsync(ProbeEvents.LedgerPosted integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }
}
