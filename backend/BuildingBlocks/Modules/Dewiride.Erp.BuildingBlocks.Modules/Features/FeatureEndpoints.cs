using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.FeatureManagement;

namespace Dewiride.Erp.BuildingBlocks.Modules.Features;

internal static class FeatureEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGroup("/platform/features")
            .WithTags("Platform.Features")
            .MapGet(string.Empty, ListAsync)
            .WithName("Platform.Features.List")
            .WithSummary("Returns every feature flag of the catalog with its evaluated state.")
            .AllowAnonymous();
    }

    private static async Task<Ok<FeaturesResponse>> ListAsync(
        FeatureCatalog catalog,
        IVariantFeatureManagerSnapshot features,
        CancellationToken cancellationToken)
    {
        var states = new List<FeatureStateResponse>(catalog.Features.Count);
        foreach (var feature in catalog.Features)
        {
            states.Add(new FeatureStateResponse(feature.Name, await features.IsEnabledAsync(feature.Name, cancellationToken).ConfigureAwait(false)));
        }

        return TypedResults.Ok(new FeaturesResponse(states));
    }
}
