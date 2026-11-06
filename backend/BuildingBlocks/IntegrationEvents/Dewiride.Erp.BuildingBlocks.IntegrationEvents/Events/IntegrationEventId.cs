using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;

public readonly record struct IntegrationEventId(Guid Value) : IStronglyTypedId<IntegrationEventId>
{
    public static IntegrationEventId Create() => new(Guid.CreateVersion7());

    public static IntegrationEventId From(Guid value) => new(value);
}
