using System.Reflection;
using Dewiride.Erp.BuildingBlocks.Configuration;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.DesignTime;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Host.Composition;

public static class ErpHostComposition
{
    public static IHostApplicationBuilder AddErpPlatform(this IHostApplicationBuilder builder, Assembly hostAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(hostAssembly);

        builder.UseOfflineConfigurationAtDesignTime();
        builder.AddErpConfiguration(hostAssembly);
        builder.AddErpPersistence();
        builder.AddModules(Modules.All);

        return builder;
    }
}
