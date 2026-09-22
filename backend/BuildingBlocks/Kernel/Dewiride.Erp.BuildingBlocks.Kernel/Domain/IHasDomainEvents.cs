using System.Collections.Generic;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Domain;

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
