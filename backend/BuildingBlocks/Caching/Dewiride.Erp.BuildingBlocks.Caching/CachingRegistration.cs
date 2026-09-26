using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Caching;

public static class CachingRegistration
{
    public static IHostApplicationBuilder AddErpCaching(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<CachingOptions>()
            .BindConfiguration(CachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IConfigureOptions<HybridCacheOptions>, HybridCacheOptionsSetup>();
        builder.Services.AddHybridCache();

        return builder;
    }
}
