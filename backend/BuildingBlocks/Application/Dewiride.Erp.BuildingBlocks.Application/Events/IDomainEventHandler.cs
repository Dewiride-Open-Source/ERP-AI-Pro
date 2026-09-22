using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Application.Events;

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
