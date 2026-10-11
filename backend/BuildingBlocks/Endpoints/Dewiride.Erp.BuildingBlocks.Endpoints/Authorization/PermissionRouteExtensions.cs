using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;

public static class PermissionRouteExtensions
{
    // A route's own policy replaces the fallback policy, so this one requires a signed-in caller too: without that, a caller
    // nobody signed in would pass whenever the requirement succeeded. A route requires one permission: every policy applies,
    // so a second call, on its group or itself, would demand both.
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        builder.Add(static endpoint =>
        {
            if (endpoint.Metadata.OfType<PermissionMetadata>().Any())
            {
                throw new InvalidOperationException($"{endpoint.DisplayName} calls RequirePermission more than once; a route requires one permission.");
            }
        });

        return builder
            .RequireAuthorization(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build())
            .WithMetadata(new PermissionMetadata(permission))
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
