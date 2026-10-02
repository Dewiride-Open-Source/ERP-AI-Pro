using Dewiride.Erp.BuildingBlocks.Caching.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Caching;

public static class CachingRegistration
{
    public const string SqlServerCacheKey = "sql-server";

    // The SQL Server cache is registered under a key only: HybridCache adopts any unkeyed IDistributedCache as its second
    // level, and it stays in process (ADR-0019). Every host composes the context, so the migrator creates the table.
    public static IHostApplicationBuilder AddErpCaching(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<CachingOptions>()
            .BindConfiguration(CachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IConfigureOptions<HybridCacheOptions>, HybridCacheOptionsSetup>();
        builder.Services.AddHybridCache();

        builder.AddModuleDbContext<CachingDbContext>(CachingDbContext.SchemaName);
        builder.Services.AddSingleton<IConfigureOptions<SqlServerCacheOptions>, SqlServerCacheOptionsSetup>();
        builder.Services.AddKeyedSingleton<IDistributedCache>(SqlServerCacheKey, static (provider, _) => new RetryingSqlServerCache(
            new SqlServerCache(provider.GetRequiredService<IOptionsMonitor<SqlServerCacheOptions>>().Get(SqlServerCacheKey)),
            RetryingSqlServerCache.CreateRetryProvider(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value),
            provider.GetRequiredService<ILogger<RetryingSqlServerCache>>()));

        return builder;
    }
}
