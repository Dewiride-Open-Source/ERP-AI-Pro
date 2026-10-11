using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Testing.Authorization;

public static class RoutePermissions
{
    public static string? Of(IServiceProvider services, string method, string route)
    {
        ArgumentNullException.ThrowIfNull(services);

        var endpoints = services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => string.Equals(PathOf(endpoint), route, StringComparison.Ordinal)
                && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase) == true)
            .ToList();

        return endpoints.Count == 1
            ? endpoints[0].Metadata.GetMetadata<PermissionMetadata>()?.Permission
            : throw new InvalidOperationException($"The host maps {endpoints.Count} endpoints for {method} {route}; expected exactly one.");
    }

    private static string PathOf(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText is { Length: > 1 } pattern ? pattern.TrimEnd('/') : endpoint.RoutePattern.RawText ?? string.Empty;
}
