using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Domain;

public readonly record struct NumberSeriesYearId(Guid Value) : IStronglyTypedId<NumberSeriesYearId>
{
    public static NumberSeriesYearId Create() => new(Guid.CreateVersion7());

    public static NumberSeriesYearId From(Guid value) => new(value);
}
