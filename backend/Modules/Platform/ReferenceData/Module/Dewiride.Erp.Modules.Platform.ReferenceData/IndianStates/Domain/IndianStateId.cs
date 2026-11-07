using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;

internal readonly record struct IndianStateId(Guid Value) : IStronglyTypedId<IndianStateId>
{
    public static IndianStateId Create() => new(Guid.CreateVersion7());

    public static IndianStateId From(Guid value) => new(value);
}
