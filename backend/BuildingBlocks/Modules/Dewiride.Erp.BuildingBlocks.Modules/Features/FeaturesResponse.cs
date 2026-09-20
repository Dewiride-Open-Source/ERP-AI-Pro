namespace Dewiride.Erp.BuildingBlocks.Modules.Features;

/// <summary>Every flag of the feature catalog with its evaluated state.</summary>
/// <param name="Features">Module and capability flags in catalog order.</param>
internal sealed record FeaturesResponse(IReadOnlyList<FeatureStateResponse> Features);
