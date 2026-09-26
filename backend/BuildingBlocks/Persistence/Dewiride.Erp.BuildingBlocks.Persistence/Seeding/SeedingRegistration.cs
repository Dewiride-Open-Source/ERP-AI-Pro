using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Seeding;

public static class SeedingRegistration
{
    public static IServiceCollection AddSeeder<TSeeder>(this IServiceCollection services, int order)
        where TSeeder : class, ISeeder
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TSeeder>();
        services.AddSingleton(new SeederRegistration(typeof(TSeeder), order));

        return services;
    }
}
