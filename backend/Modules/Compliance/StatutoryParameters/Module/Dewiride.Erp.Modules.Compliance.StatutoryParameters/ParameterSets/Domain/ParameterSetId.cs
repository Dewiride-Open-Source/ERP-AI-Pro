using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;

internal readonly record struct ParameterSetId(Guid Value) : IStronglyTypedId<ParameterSetId>
{
    public static ParameterSetId Create() => new(Guid.CreateVersion7());

    public static ParameterSetId From(Guid value) => new(value);
}
