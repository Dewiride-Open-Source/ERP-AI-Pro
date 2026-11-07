using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;

internal readonly record struct ParameterVersionId(Guid Value) : IStronglyTypedId<ParameterVersionId>
{
    public static ParameterVersionId Create() => new(Guid.CreateVersion7());

    public static ParameterVersionId From(Guid value) => new(value);
}
