using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.SystemInfo.Contracts.Info;
using Dewiride.Erp.Modules.Platform.SystemInfo.Info.Endpoints;
using Dewiride.Erp.Modules.Platform.SystemInfo.PublicApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Modules.Platform.SystemInfo;

public sealed class SystemInfoModule : IModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Platform",
        Name: "SystemInfo",
        Schema: null,
        RoutePrefix: "/platform/system-info",
        FeatureFlag: "Erp.Modules.Platform.SystemInfo",
        Permissions: []);

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<ISystemInfoQueries, SystemInfoQueries>();
        builder.Services.AddHandlersFromAssembly(typeof(SystemInfoModule).Assembly);
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        SystemInfoEndpoints.Map(group);
    }
}
