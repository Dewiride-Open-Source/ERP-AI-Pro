using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Retention;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;
using Dewiride.Erp.BuildingBlocks.Jobs.Telemetry;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Jobs;

public static class JobsRegistration
{
    public static IHostApplicationBuilder AddErpJobs(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<JobsOptions>()
            .BindConfiguration(JobsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.AddModuleDbContext<JobsDbContext>(JobsDbContext.SchemaName);
        builder.Services.TryAddSingleton(provider => new JobCatalog(provider.GetServices<JobRegistration>()));
        builder.Services.TryAddSingleton<JobSignal>();
        builder.Services.TryAddSingleton<JobsMetrics>();
        builder.Services.TryAddScoped<IJobQueue, SqlJobQueue>();
        builder.Services.TryAddScoped<IJobAdministration, JobAdministration>();
        builder.Services.AddRetentionSweep<JobRunRetentionSweep>();

        return builder;
    }

    // Only the API runs jobs: the migrator composes the same modules without the services a job may need, such as the directory,
    // and never starts its host.
    public static IHostApplicationBuilder AddErpJobRunner(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<JobRunnerOptions>()
            .BindConfiguration(JobRunnerOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.TryAddSingleton(_ => JobRunnerInstance.ForThisProcess());
        builder.Services.TryAddSingleton<JobInvoker>();
        builder.Services.TryAddSingleton<JobRunner>();
        builder.Services.TryAddSingleton<JobScheduler>();
        builder.Services.AddHostedService<JobRunnerService>();

        return builder;
    }
}
