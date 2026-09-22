using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.SystemInfo;

namespace Dewiride.Erp.Host.Composition;

public static class Modules
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new SystemInfoModule(),
    ];
}
