using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;

internal readonly record struct ParameterRowId(Guid Value) : IStronglyTypedId<ParameterRowId>
{
    public static ParameterRowId Create() => new(Guid.CreateVersion7());

    public static ParameterRowId From(Guid value) => new(value);
}
