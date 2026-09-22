using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.Modules.Platform.SystemInfo.Contracts.Info;
using Dewiride.Erp.Modules.Platform.SystemInfo.Info.Endpoints;
using Dewiride.Erp.Modules.Platform.SystemInfo.Persistence;
using Dewiride.Erp.Modules.Platform.SystemInfo.PublicApi;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Endpoints;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Modules.Platform.SystemInfo;

public sealed class SystemInfoModule : IModule
{
    public const string FeatureFlag = "Erp.Modules.Platform.SystemInfo";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Platform",
        Name: "SystemInfo",
        Schema: SystemInfoDbContext.SchemaName,
        RoutePrefix: "/platform/system-info",
        FeatureFlag: FeatureFlag,
        Permissions: [],
        Capabilities: []);

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<SystemInfoDbContext>(SystemInfoDbContext.SchemaName);
        builder.Services.AddSingleton<ISystemInfoQueries, SystemInfoQueries>();
        builder.Services.AddHandlersFromAssembly(typeof(SystemInfoModule).Assembly);
        builder.Services.AddSingleton<StartupRecorder>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<StartupRecorder>());
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        SystemInfoEndpoints.Map(group);
        StartupEndpoints.Map(group);
    }
}
