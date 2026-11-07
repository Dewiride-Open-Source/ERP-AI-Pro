using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Caching;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts;
using Dewiride.Erp.Modules.Platform.ReferenceData.Currencies.Application.Queries.ListCurrencies;
using Dewiride.Erp.Modules.Platform.ReferenceData.Currencies.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.ReferenceData.Shared.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Currencies.Endpoints;

internal static class CurrencyEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/currencies", ListAsync)
            .WithName("Platform.ReferenceData.ListCurrencies")
            .WithSummary("Lists every currency an amount in the ERP can be in, by code, with its name and decimal places.")
            .RequirePermission(ReferenceDataPermissions.ReadLists)
            .WithReferenceDataCaching(ReferenceListCaching.MaxAge);
    }

    private static async Task<Results<Ok<CurrenciesResponse>, ProblemHttpResult>> ListAsync(
        IQueryHandler<ListCurrenciesQuery, IReadOnlyList<Currency>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListCurrenciesQuery(), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(new CurrenciesResponse([.. result.Value.Select(currency => new CurrencyResponse(currency.Code, currency.Name, currency.MinorUnits))]))
            : result.Error!.ToProblem();
    }
}
