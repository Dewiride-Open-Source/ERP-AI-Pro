using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.Observability.Health;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Persistence;

public static class ModuleDbContextRegistration
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    public const int CompatibilityLevel = 170;

    public static IHostApplicationBuilder AddModuleDbContext<TContext>(this IHostApplicationBuilder builder, string schema)
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddModuleDbContext<TContext>(schema);

        return builder;
    }

    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        services.AddDbContext<TContext>((provider, options) =>
        {
            Configure(options, provider.GetRequiredService<IOptions<DatabaseOptions>>().Value, schema)
                .AddInterceptors(provider.GetRequiredService<AuditingSaveChangesInterceptor>(), provider.GetRequiredService<BulkWriteGuardInterceptor>());
            // Test hosts are built many times per process, each with its own interceptor instances, so EF Core's
            // many-internal-providers warning is expected there; the raw setting still marks a test host whose test swaps in
            // another ErpConfigurationInfo.
            if (string.Equals(provider.GetService<IConfiguration>()?[ErpConfigurationSourceResolver.SourceSetting], ErpConfigurationSourceResolver.InMemorySource, StringComparison.OrdinalIgnoreCase))
            {
                options.ConfigureWarnings(warnings => warnings.Log(CoreEventId.ManyServiceProvidersCreatedWarning));
            }
        });
        services.AddSingleton(new DbContextRegistration(typeof(TContext), schema));
        services.AddHealthChecks().AddDbContextCheck<TContext>($"database:{schema}", tags: [HealthEndpoints.ReadyTag]);

        return services;
    }

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, DatabaseOptions database, string schema)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        var commandTimeout = (int)database.CommandTimeout.TotalSeconds;
        if (database.Provider == DatabaseProvider.AzureSql)
        {
            options.UseAzureSql(database.ConnectionString, sql => sql
                .MigrationsHistoryTable(MigrationsHistoryTable, schema)
                .CommandTimeout(commandTimeout)
                .EnableRetryOnFailure(database.MaxRetryCount, database.MaxRetryDelay, errorNumbersToAdd: null));
            return options;
        }

        return options.UseSqlServer(database.ConnectionString, sql => sql
            .MigrationsHistoryTable(MigrationsHistoryTable, schema)
            .CommandTimeout(commandTimeout)
            .UseCompatibilityLevel(CompatibilityLevel)
            .EnableRetryOnFailure(database.MaxRetryCount, database.MaxRetryDelay, errorNumbersToAdd: null));
    }
}
