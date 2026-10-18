using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
using Dewiride.Erp.Modules.Identity.Users.People.Application;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints;

// The check reads the directory with the ERP's own permission; the endpoint hands it the directory of the request's
// services, as the invitations hand theirs, so the hosts without the sign-in services compose the check too. A repeated check
// finds the people it deactivated inactive already and leaves them as they are, so it takes no idempotency key.
internal static class AccountCheckEndpoints
{
    public static void Map(RouteGroupBuilder people)
    {
        people.MapPost("/directory/account-check", CheckAsync)
            .WithName("Identity.Users.CheckDirectoryAccounts")
            .WithSummary("Reads the Entra accounts of the active people with the ERP's own permission and deactivates everyone whose account is disabled or deleted; their sessions end at their next request.")
            .RequirePermission(UsersPermissions.ChangeStatus)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<Results<Ok<DirectoryAccountCheckResponse>, ProblemHttpResult>> CheckAsync(
        IDirectoryAccounts directory,
        DirectoryAccountCheck check,
        CancellationToken cancellationToken)
    {
        var result = await check.RunAsync(directory, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        return TypedResults.Ok(new DirectoryAccountCheckResponse(
            result.Value.CheckedCount,
            [.. result.Value.Deactivated.Select(person => new DeactivatedPersonResponse(person.Id, person.DisplayName, person.DeactivatedBecause))]));
    }
}
