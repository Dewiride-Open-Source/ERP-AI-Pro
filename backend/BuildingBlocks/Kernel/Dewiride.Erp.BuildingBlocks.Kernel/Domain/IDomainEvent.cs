using System;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Domain;

public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}
