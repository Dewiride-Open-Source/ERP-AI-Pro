using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.Kernel.Events;
using Dewiride.Erp.BuildingBlocks.Persistence;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Publishing;

// Writes the event into the outbox of the module whose context is given, in the transaction of the change it describes, so the
// event exists exactly when the change does. A module calls it from a domain-event handler in its Integration/Publishers folder
// while the command's unit of work is open, after the module has decided the person may make the change: the subscribers run as
// the system and check nothing of a person. The job runner passes the event on once the transaction has committed.
public interface IIntegrationEventPublisher
{
    IntegrationEventId Publish<TEvent>(ModuleDbContext context, TEvent integrationEvent)
        where TEvent : class, IIntegrationEvent;
}
