using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.BuildingBlocks.Persistence.Telemetry;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Persistence;

public static class PersistenceRegistration
{
    public static IHostApplicationBuilder AddErpPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddErpPersistenceCore();
        builder.Services.AddErpDatabaseTelemetry();

        return builder;
    }

    public static IServiceCollection AddErpPersistenceCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>());
        services.TryAddSingleton(provider => new DbContextCatalog(provider.GetServices<DbContextRegistration>()));
        services.TryAddSingleton<DatabaseMigrator>();
        services.TryAddSingleton<SeedRunner>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IActorContext, SystemActorContext>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();
        services.TryAddSingleton<BulkWriteGuardInterceptor>();
        services.AddApplicationPipeline();
        services.TryAddScoped<UnitOfWorkSignal>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPipelineStep, UnitOfWorkStep>());

        return services;
    }
}
