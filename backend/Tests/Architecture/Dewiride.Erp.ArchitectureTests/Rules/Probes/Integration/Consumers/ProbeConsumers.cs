using Dewiride.Erp.ArchitectureTests.Rules.Probes.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.ArchitectureTests.Rules.Probes.Integration.Consumers;

// Subscribers placed as a module's Integration/Consumers folder places them, one keeping the rules, one breaking the placement
// and one asking for a service the API host does not compose.
public static class ProbeConsumers
{
    public interface ILedgerBook
    {
        bool Holds(Guid ledgerId);
    }

    public sealed class LedgerPostedRecorder : IIntegrationEventHandler<ProbeEvents.LedgerPosted>
    {
        public Task<Result> HandleAsync(ProbeEvents.LedgerPosted integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }

    public class LedgerPostedAuditor : IIntegrationEventHandler<ProbeEvents.LedgerPosted>
    {
        public Task<Result> HandleAsync(ProbeEvents.LedgerPosted integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }

    public sealed class LedgerPostedChecker(ILedgerBook book) : IIntegrationEventHandler<ProbeEvents.LedgerPosted>
    {
        public Task<Result> HandleAsync(ProbeEvents.LedgerPosted integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken) =>
            Task.FromResult(book.Holds(integrationEvent?.LedgerId ?? Guid.Empty) ? Result.Success() : Result.Fail(Error.NotFound("probe.ledger-unknown", "The ledger is not in the book.")));
    }
}
