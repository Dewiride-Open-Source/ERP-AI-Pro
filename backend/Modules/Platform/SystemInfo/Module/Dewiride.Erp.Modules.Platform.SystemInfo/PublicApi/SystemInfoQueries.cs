using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.Modules.Platform.SystemInfo.Contracts.Info;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.PublicApi;

internal sealed class SystemInfoQueries(ApplicationInfo application) : ISystemInfoQueries
{
    public SystemInfoSummary GetSummary() => new(application.Name, application.Version, application.StartedAt);
}
