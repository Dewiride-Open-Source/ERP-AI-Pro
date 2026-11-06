using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Definitions;

// A job is not a service: the runner activates it for each attempt in the attempt's own scope, so its constructor may ask for a
// service only the API composes, such as the directory, while every other host still composes the module that declares it.
// A recurring job reads its schedule from its module's options at every poll of the scheduler, so a refreshed setting applies
// without a restart.
public static class JobServiceCollectionExtensions
{
    public static IServiceCollection AddJob<TJob>(this IServiceCollection services, JobDefinition definition)
        where TJob : class, IJob =>
        services.AddJobRegistration(new JobRegistration(definition, typeof(TJob), null, static (job, _, context, cancellationToken) => ((TJob)job).RunAsync(context, cancellationToken), null));

    public static IServiceCollection AddJob<TJob, TPayload>(this IServiceCollection services, JobDefinition definition)
        where TJob : class, IJob<TPayload>
        where TPayload : class =>
        services.AddJobRegistration(new JobRegistration(
            definition,
            typeof(TJob),
            typeof(TPayload),
            static (job, payload, context, cancellationToken) => ((TJob)job).RunAsync((TPayload)payload!, context, cancellationToken),
            null));

    public static IServiceCollection AddRecurringJob<TJob, TOptions>(this IServiceCollection services, JobDefinition definition, Func<TOptions, string> schedule)
        where TJob : class, IJob
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return services.AddJobRegistration(new JobRegistration(
            definition,
            typeof(TJob),
            null,
            static (job, _, context, cancellationToken) => ((TJob)job).RunAsync(context, cancellationToken),
            provider => schedule(provider.GetRequiredService<IOptionsMonitor<TOptions>>().CurrentValue)));
    }

    public static IServiceCollection AddJobRunSource<TSource>(this IServiceCollection services)
        where TSource : class, IJobRunSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TSource>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IJobRunSource, TSource>(provider => provider.GetRequiredService<TSource>()));

        return services;
    }

    private static IServiceCollection AddJobRegistration(this IServiceCollection services, JobRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registration.Definition);

        registration.Definition.EnsureValid();
        var registered = services
            .Where(descriptor => descriptor.ServiceType == typeof(JobRegistration) && !descriptor.IsKeyedService)
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<JobRegistration>();
        foreach (var existing in registered)
        {
            if (string.Equals(existing.Name, registration.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The job {registration.Name} is registered more than once.");
            }

            if (existing.JobType == registration.JobType)
            {
                throw new InvalidOperationException($"{registration.JobType.Name} is registered as both {existing.Name} and {registration.Name}.");
            }
        }

        services.AddSingleton(registration);

        return services;
    }
}
