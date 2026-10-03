using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Dewiride.Erp.BuildingBlocks.Modules.Features;

public sealed class FeatureCatalog
{
    public const string PlatformFlagPrefix = "Erp.Platform.";

    private readonly Dictionary<string, FeatureDescriptor> _features = new(StringComparer.OrdinalIgnoreCase);

    public FeatureCatalog(ModuleCatalog modules, IEnumerable<FeatureDescriptor> platformFeatures)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(platformFeatures);

        foreach (var descriptor in modules.Modules.Select(m => m.Descriptor))
        {
            Add(new FeatureDescriptor(descriptor.FeatureFlag, EnabledByDefault: true));
            foreach (var capability in descriptor.Capabilities)
            {
                Add(new FeatureDescriptor($"{descriptor.FeatureFlag}.{capability.Name}", capability.EnabledByDefault));
            }
        }

        foreach (var feature in platformFeatures)
        {
            if (!feature.Name.StartsWith(PlatformFlagPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Platform feature flag '{feature.Name}' must start with '{PlatformFlagPrefix}'.");
            }

            Add(feature);
        }

        Features = [.. _features.Values];
    }

    public IReadOnlyList<FeatureDescriptor> Features { get; }

    public bool TryGet(string name, [NotNullWhen(true)] out FeatureDescriptor? feature) => _features.TryGetValue(name, out feature);

    private void Add(FeatureDescriptor feature)
    {
        if (feature.Name.Contains(ConfigurationPath.KeyDelimiter, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Feature flag '{feature.Name}' must not contain '{ConfigurationPath.KeyDelimiter}'.");
        }

        if (!_features.TryAdd(feature.Name, feature))
        {
            throw new InvalidOperationException($"Feature flag '{feature.Name}' is declared more than once.");
        }
    }
}
