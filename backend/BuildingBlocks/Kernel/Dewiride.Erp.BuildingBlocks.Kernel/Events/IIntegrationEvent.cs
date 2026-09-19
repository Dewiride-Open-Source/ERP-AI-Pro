using System;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Events;

public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredOn { get; }
}
