using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Persistence.DesignTime;

public static class DesignTimeConfiguration
{
    public static IHostApplicationBuilder UseOfflineConfigurationAtDesignTime(this IHostApplicationBuilder builder) =>
        Apply(builder, EF.IsDesignTime);

    internal static IHostApplicationBuilder Apply(IHostApplicationBuilder builder, bool isDesignTime)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (isDesignTime)
        {
            builder.Configuration[ErpConfigurationSourceResolver.SourceSetting] = ErpConfigurationSourceResolver.LocalDevelopmentSource;
        }

        return builder;
    }
}
