using Microsoft.AspNetCore.Builder;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Caching;

// Only reference data that is the same for every caller may be marked: the browser keeps the response for the max age.
public static class ReferenceDataCachingExtensions
{
    private static readonly TimeSpan MaximumMaxAge = TimeSpan.FromDays(1);

    public static TBuilder WithReferenceDataCaching<TBuilder>(this TBuilder builder, TimeSpan maxAge)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAge, TimeSpan.FromSeconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxAge, MaximumMaxAge);

        return builder.WithMetadata(new ReferenceDataCacheMetadata(maxAge));
    }
}
