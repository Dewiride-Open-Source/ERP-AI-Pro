using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
using Dewiride.Erp.Modules.Identity.Users.Permissions.Application;
using Dewiride.Erp.Modules.Identity.Users.Permissions.Application.Queries.ListPermissions;
using Dewiride.Erp.Modules.Identity.Users.Permissions.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Identity.Users.Permissions.Endpoints;

// What each role may do is read beside the people it is assigned to, so the catalogue needs the permission to read people
// rather than one of its own.
internal static class PermissionEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/permissions", ListAsync)
            .WithName("Identity.Users.ListPermissions")
            .WithSummary("Lists every permission of the ERP in ordinal order, with what it lets a holder do and the built-in roles that hold it.")
            .RequirePermission(UsersPermissions.Read);
    }

    private static PermissionCatalogResponse ToResponse(PermissionCatalogDetails details) =>
        new(details.Roles, [.. details.Permissions.Select(permission => new PermissionResponse(permission.Name, permission.Description, permission.HeldBy))]);

    private static async Task<Results<Ok<PermissionCatalogResponse>, ProblemHttpResult>> ListAsync(
        IQueryHandler<ListPermissionsQuery, PermissionCatalogDetails> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListPermissionsQuery(), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }
}
