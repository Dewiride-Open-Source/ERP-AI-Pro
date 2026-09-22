using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;

internal readonly record struct ApiStartupId(Guid Value) : IStronglyTypedId<ApiStartupId>
{
    public static ApiStartupId Create() => new(Guid.CreateVersion7());

    public static ApiStartupId From(Guid value) => new(value);
}
