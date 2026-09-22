using System;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Domain;

public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }

    Guid? DeletedBy { get; }
}
