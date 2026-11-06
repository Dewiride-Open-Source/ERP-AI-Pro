using Dewiride.Erp.BuildingBlocks.Kernel.Events;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;

// A module's answer to an integration event, another module's or its own, in its Integration/Consumers folder. It runs at least
// once per event, as the system, in its own module's unit of work, which also records that it handled the event; it turns the
// event into a command of its own module, reads the current state, and changes nothing outside its database that it could not
// safely change twice.
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : class, IIntegrationEvent
{
    Task<Result> HandleAsync(TEvent integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken);
}
