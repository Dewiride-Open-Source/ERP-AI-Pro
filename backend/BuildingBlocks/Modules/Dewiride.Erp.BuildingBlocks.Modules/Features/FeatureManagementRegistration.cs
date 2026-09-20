using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Dewiride.Erp.BuildingBlocks.Modules.Features;

internal static class FeatureManagementRegistration
{
    public static IServiceCollection AddErpFeatureManagement(this IServiceCollection services)
    {
        services.AddSingleton<FeatureCatalog>();
        services.Configure<ConfigurationFeatureDefinitionProviderOptions>(options => options.CustomConfigurationMergingEnabled = true);
        services.AddSingleton<IFeatureDefinitionProvider, CatalogFeatureDefinitionProvider>();
        services.AddFeatureManagement();

        return services;
    }
}
