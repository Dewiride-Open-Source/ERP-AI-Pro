using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;

// A person's token is granted by its scopes alone and an application's by its roles alone: a person's roles are their own
// app roles, which must never stand in for an application's. Microsoft.Identity.Web's RequireScopeOrAppPermission accepts
// any matching roles value, a person's own included.
internal sealed class BearerTokenAccessHandler : AuthorizationHandler<BearerTokenAccessRequirement>
{
    public static bool IsGranted(ClaimsPrincipal token, BearerTokenAccess access)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(access);

        return BearerTokenClaims.HolderOf(token) switch
        {
            BearerTokenHolder.Person => BearerTokenClaims.ScopesOf(token).Any(scope => access.Scopes.Contains(scope, StringComparer.Ordinal)),
            BearerTokenHolder.Application => BearerTokenClaims.RolesOf(token).Any(role => access.ApplicationRoles.Contains(role, StringComparer.Ordinal)),
            _ => false,
        };
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, BearerTokenAccessRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (IsGranted(context.User, requirement.Access))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
