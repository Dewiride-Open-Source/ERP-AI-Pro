using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

namespace Dewiride.Erp.BuildingBlocks.Modules.Features;

internal sealed class CatalogFeatureDefinitionProvider : IFeatureDefinitionProvider, IDisposable
{
    private const string AlwaysOnFilter = "AlwaysOn";

    private readonly FeatureCatalog _catalog;
    private readonly ConfigurationFeatureDefinitionProvider _configured;

    public CatalogFeatureDefinitionProvider(
        FeatureCatalog catalog,
        IConfiguration configuration,
        IOptions<ConfigurationFeatureDefinitionProviderOptions> options,
        ILogger<CatalogFeatureDefinitionProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _catalog = catalog;
        _configured = new ConfigurationFeatureDefinitionProvider(configuration, options.Value)
        {
            RootConfigurationFallbackEnabled = false,
            Logger = logger,
        };
    }

    public async Task<FeatureDefinition> GetFeatureDefinitionAsync(string featureName)
    {
        var configured = await _configured.GetFeatureDefinitionAsync(featureName).ConfigureAwait(false);
        if (configured is not null)
        {
            return configured;
        }

        return _catalog.TryGet(featureName, out var feature) ? CatalogDefault(feature) : null!;
    }

    public async IAsyncEnumerable<FeatureDefinition> GetAllFeatureDefinitionsAsync()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await foreach (var definition in _configured.GetAllFeatureDefinitionsAsync().ConfigureAwait(false))
        {
            seen.Add(definition.Name);
            yield return definition;
        }

        foreach (var feature in _catalog.Features.Where(f => !seen.Contains(f.Name)))
        {
            yield return CatalogDefault(feature);
        }
    }

    public void Dispose() => _configured.Dispose();

    private static FeatureDefinition CatalogDefault(FeatureDescriptor feature) => feature.EnabledByDefault
        ? new FeatureDefinition
        {
            Name = feature.Name,
            Status = FeatureStatus.Conditional,
            EnabledFor = [new FeatureFilterConfiguration { Name = AlwaysOnFilter }],
        }
        : new FeatureDefinition { Name = feature.Name, Status = FeatureStatus.Disabled };
}
