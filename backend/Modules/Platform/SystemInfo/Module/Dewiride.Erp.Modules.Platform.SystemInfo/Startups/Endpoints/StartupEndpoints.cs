using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Application.Queries.ListRecentStartups;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Endpoints;

internal static class StartupEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/startups", ListAsync)
            .WithName("Platform.SystemInfo.ListStartups")
            .WithSummary("Lists the 20 most recent starts of the API, newest first.")
            .AllowAnonymous();
    }

    private static async Task<Results<Ok<RecentStartupsResponse>, ProblemHttpResult>> ListAsync(
        IQueryHandler<ListRecentStartupsQuery, IReadOnlyList<StartupDetails>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListRecentStartupsQuery(), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(new RecentStartupsResponse(result.Value.Select(ToResponse).ToArray()))
            : result.Error!.ToProblem();
    }

    private static StartupResponse ToResponse(StartupDetails details) =>
        new(details.Id, details.ApplicationName, details.Version, details.Framework, details.EnvironmentName, details.ConfigurationLabel, details.StartedAt, details.RecordedAt);
}
