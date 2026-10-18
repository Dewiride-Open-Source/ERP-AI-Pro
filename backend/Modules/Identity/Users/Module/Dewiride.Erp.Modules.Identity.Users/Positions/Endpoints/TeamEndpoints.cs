using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.ListTeam;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints;

// A team includes the people planned to report to the person from a later date, which is the history of their positions, so
// reading it needs the permission to read that history.
internal static class TeamEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/team", ListAsync)
            .WithName("Identity.Users.ListTeam")
            .WithSummary("Lists the people whose position names a person as their manager today or from a later date, the team a deactivation of that person hands over, by display name.")
            .RequirePermission(UsersPermissions.ReadPositions);
    }

    private static async Task<Results<Ok<IReadOnlyList<TeamMemberResponse>>, ProblemHttpResult>> ListAsync(
        Guid id,
        IQueryHandler<ListTeamQuery, IReadOnlyList<TeamMemberDetails>> handler,
        CancellationToken cancellationToken)
    {
        var team = await handler.HandleAsync(new ListTeamQuery(id), cancellationToken);
        if (team.IsFailure)
        {
            return team.Error!.ToProblem();
        }

        IReadOnlyList<TeamMemberResponse> members = [.. team.Value.Select(member => new TeamMemberResponse(member.Id, member.DisplayName))];

        return TypedResults.Ok(members);
    }
}
