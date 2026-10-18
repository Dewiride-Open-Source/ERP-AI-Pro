using Microsoft.AspNetCore.Builder;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Caching;

// For data that differs by caller, such as a person's own photo: the browser keeps the response for the max age, and reuses it
// only for a request signed in by the same cookie or bearer token, so another person signed in on the same browser never
// sees it.
public static class CallerDataCachingExtensions
{
    private static readonly TimeSpan MaximumMaxAge = TimeSpan.FromDays(1);

    public static TBuilder WithCallerDataCaching<TBuilder>(this TBuilder builder, TimeSpan maxAge)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAge, TimeSpan.FromSeconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxAge, MaximumMaxAge);

        return builder.WithMetadata(new CallerDataCacheMetadata(maxAge));
    }
}
