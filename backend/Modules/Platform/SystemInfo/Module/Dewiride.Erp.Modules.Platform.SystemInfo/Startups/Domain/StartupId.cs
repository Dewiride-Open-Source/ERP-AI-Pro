using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;

internal readonly record struct StartupId(Guid Value) : IStronglyTypedId<StartupId>
{
    public static StartupId Create() => new(Guid.CreateVersion7());

    public static StartupId From(Guid value) => new(value);
}
