using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Caching;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application.Queries.GetParameterSet;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application.Queries.ListParameterSets;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Requests;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Shared.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints;

// A seeded set is addressed by its key, which never changes, rather than by the id of its row.
internal static class ParameterSetEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var routes = group.MapGroup("/parameter-sets");

        routes.MapGet("/", ListAsync)
            .WithName("Compliance.StatutoryParameters.ListParameterSets")
            .WithSummary("Lists the statutory parameter sets by title, each with the version in force today in India and the next one.")
            .RequirePermission(StatutoryParametersPermissions.ReadParameterSets)
            .WithCallerDataCaching(ParameterSetCaching.MaxAge);

        routes.MapGet("/{key}", GetAsync)
            .WithName("Compliance.StatutoryParameters.GetParameterSet")
            .WithSummary("Describes one statutory parameter set: its columns, its versions with what each changed, and the entries and changes of the version in force on a date.")
            .RequirePermission(StatutoryParametersPermissions.ReadParameterSets)
            .WithCallerDataCaching(ParameterSetCaching.MaxAge);
    }

    private static async Task<Results<Ok<ParameterSetsResponse>, ProblemHttpResult>> ListAsync(
        IQueryHandler<ListParameterSetsQuery, IReadOnlyList<ParameterSetEntry>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListParameterSetsQuery(), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ParameterSetResponses.Of(result.Value)) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ParameterSetResponse>, ProblemHttpResult>> GetAsync(
        [AsParameters] GetParameterSetRequest request,
        IQueryHandler<GetParameterSetQuery, ShownParameterSet> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetParameterSetQuery(request.Key, request.On), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ParameterSetResponses.Of(result.Value)) : result.Error!.ToProblem();
    }
}
