using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.SystemInfo.Info.Application.Queries.GetSystemInfo;
using Dewiride.Erp.Modules.Platform.SystemInfo.Info.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Info.Endpoints;

internal static class SystemInfoEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet(string.Empty, GetAsync)
            .WithName("Platform.SystemInfo.Get")
            .WithSummary("Returns the application name, version and uptime of the API.")
            .AllowAnonymous();
    }

    private static async Task<Results<Ok<SystemInfoResponse>, ProblemHttpResult>> GetAsync(
        IQueryHandler<GetSystemInfoQuery, SystemInfoDetails> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetSystemInfoQuery(), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(new SystemInfoResponse(result.Value.ApplicationName, result.Value.Version, result.Value.StartedAt, result.Value.Uptime.TotalSeconds))
            : result.Error!.ToProblem();
    }
}
