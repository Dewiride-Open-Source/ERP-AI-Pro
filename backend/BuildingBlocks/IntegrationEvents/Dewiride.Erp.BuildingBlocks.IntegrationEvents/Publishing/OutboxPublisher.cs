using Dewiride.Erp.BuildingBlocks.Application.Origins;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Relay;
using Dewiride.Erp.BuildingBlocks.Kernel.Events;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Publishing;

internal sealed class OutboxPublisher(UnitOfWorkSignal signal, OutboxRelay relay, IntegrationEventChain chain, IRequestOrigin origin, TimeProvider timeProvider) : IIntegrationEventPublisher
{
    public IntegrationEventId Publish<TEvent>(ModuleDbContext context, TEvent integrationEvent)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var name = IntegrationEventNames.Of<TEvent>();
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException($"{name} was published outside a unit of work; publish it from a domain-event handler of the command that makes the change, so it commits with the change.");
        }

        if (context.Model.FindEntityType(typeof(OutboxMessage)) is null)
        {
            throw new InvalidOperationException($"{context.GetType().Name} has no outbox; declare `public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();` and add its migration.");
        }

        var depth = chain.DepthOf(name);
        var id = IntegrationEventId.Create();
        context.Set<OutboxMessage>().Add(new OutboxMessage(id, name, IntegrationEventSerializer.Serialize(integrationEvent), timeProvider.GetUtcNow(), origin.CorrelationId, depth));
        signal.AfterCommit(relay.Announce);

        return id;
    }
}
