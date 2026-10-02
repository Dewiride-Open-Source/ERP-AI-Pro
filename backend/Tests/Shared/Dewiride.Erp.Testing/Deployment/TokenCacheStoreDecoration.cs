using Dewiride.Erp.BuildingBlocks.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Testing.Deployment;

public static class TokenCacheStoreDecoration
{
    // Wraps the keyed SQL Server cache the token cache stores its entries in, whichever registration is the latest; a test
    // host registers it from ConfigureServices, which runs after the API has registered its own.
    public static IServiceCollection DecorateTokenCacheStore(this IServiceCollection services, Func<IDistributedCache, IDistributedCache> decorate)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decorate);

        var current = services.LastOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IDistributedCache) && descriptor.IsKeyedService && Equals(descriptor.ServiceKey, CachingRegistration.SqlServerCacheKey))
            ?? throw new InvalidOperationException($"No IDistributedCache is registered under the key '{CachingRegistration.SqlServerCacheKey}'; AddErpCaching registers it.");

        services.Remove(current);
        services.AddKeyedSingleton<IDistributedCache>(CachingRegistration.SqlServerCacheKey, (provider, key) => decorate(Create(current, provider, key)));

        return services;
    }

    private static IDistributedCache Create(ServiceDescriptor descriptor, IServiceProvider provider, object? key) =>
        (IDistributedCache)(descriptor.KeyedImplementationInstance
            ?? descriptor.KeyedImplementationFactory?.Invoke(provider, key)
            ?? ActivatorUtilities.CreateInstance(provider, descriptor.KeyedImplementationType!));
}
