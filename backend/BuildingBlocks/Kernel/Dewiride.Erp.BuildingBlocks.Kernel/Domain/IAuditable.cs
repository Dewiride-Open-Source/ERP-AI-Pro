using System;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Domain;

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    Guid CreatedBy { get; }

    DateTimeOffset? ModifiedAt { get; }

    Guid? ModifiedBy { get; }
}
