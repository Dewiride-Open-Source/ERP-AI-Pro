using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Conventions;

public sealed class StronglyTypedIdConverter<TId> : ValueConverter<TId, Guid>
    where TId : struct, IStronglyTypedId<TId>
{
    public StronglyTypedIdConverter()
        : base(id => id.Value, value => FromGuid(value))
    {
    }

    private static TId FromGuid(Guid value) => TId.From(value);
}
