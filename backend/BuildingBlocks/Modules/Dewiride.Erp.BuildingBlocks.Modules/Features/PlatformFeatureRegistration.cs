using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.Modules.Features;

public static class PlatformFeatureRegistration
{
    public static IServiceCollection AddErpPlatformFeature(this IServiceCollection services, string name, bool enabledByDefault)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return services.AddSingleton(new FeatureDescriptor(name, enabledByDefault));
    }
}
