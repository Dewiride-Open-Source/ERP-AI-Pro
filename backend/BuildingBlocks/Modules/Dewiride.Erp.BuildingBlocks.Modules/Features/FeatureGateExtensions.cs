using Microsoft.AspNetCore.Builder;

namespace Dewiride.Erp.BuildingBlocks.Modules.Features;

public static class FeatureGateExtensions
{
    public static TBuilder RequireFeature<TBuilder>(this TBuilder builder, string featureName)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);

        return builder.WithMetadata(new RequireFeatureMetadata(featureName));
    }

    public static IApplicationBuilder UseFeatureGate(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<FeatureGateMiddleware>();
    }
}
