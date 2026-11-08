using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Domain;

public readonly record struct NumberSeriesId(Guid Value) : IStronglyTypedId<NumberSeriesId>
{
    public static NumberSeriesId Create() => new(Guid.CreateVersion7());

    public static NumberSeriesId From(Guid value) => new(value);
}
