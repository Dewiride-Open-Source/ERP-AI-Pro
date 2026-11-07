using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Caching;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Application.Queries.ListIndianStates;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Endpoints.Requests;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.ReferenceData.Shared.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Endpoints;

internal static class IndianStateEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/indian-states", ListAsync)
            .WithName("Platform.ReferenceData.ListIndianStates")
            .WithSummary("Lists India's States and Union Territories with their GST State codes, those of one date or every code with its dates.")
            .RequirePermission(ReferenceDataPermissions.ReadLists)
            .WithReferenceDataCaching(ReferenceListCaching.MaxAge);
    }

    private static async Task<Results<Ok<IndianStatesResponse>, ProblemHttpResult>> ListAsync(
        [AsParameters] ListIndianStatesRequest request,
        IQueryHandler<ListIndianStatesQuery, IReadOnlyList<IndianStateEntry>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListIndianStatesQuery(request.On), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(new IndianStatesResponse([.. result.Value.Select(state => new IndianStateResponse(state.GstCode, state.Name, state.Kind, state.ValidFrom, state.ValidUntil))]))
            : result.Error!.ToProblem();
    }
}
