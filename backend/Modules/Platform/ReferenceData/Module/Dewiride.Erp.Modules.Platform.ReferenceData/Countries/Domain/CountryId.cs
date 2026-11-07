using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Domain;

internal readonly record struct CountryId(Guid Value) : IStronglyTypedId<CountryId>
{
    public static CountryId Create() => new(Guid.CreateVersion7());

    public static CountryId From(Guid value) => new(value);
}
