using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;

public static class BearerTokenRouteExtensions
{
    // The route is signed in by a bearer token alone (RouteSignInScheme), so the session cookie neither signs it in nor needs
    // an antiforgery token there, and a route never takes both: a policy naming two schemes would challenge both on one
    // response.
    public static TBuilder RequireBearerToken<TBuilder>(this TBuilder builder, BearerTokenAccess access)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(access);

        return builder
            .RequireAuthorization(new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new BearerTokenAccessRequirement(access))
                .Build())
            .WithMetadata(new BearerTokenRouteMetadata(access));
    }
}
