using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Retention;

public static class RetentionRegistration
{
    public static IServiceCollection AddRetentionSweep<TSweep>(this IServiceCollection services)
        where TSweep : class, IRetentionSweep
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TSweep>();
        services.AddSingleton(new RetentionSweepRegistration(typeof(TSweep)));

        return services;
    }
}
