using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Caching;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.Countries;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Application.Queries.ListCountries;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.ReferenceData.Shared.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Endpoints;

internal static class CountryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/countries", ListAsync)
            .WithName("Platform.ReferenceData.ListCountries")
            .WithSummary("Lists every country the ERP knows by name, with its ISO 3166-1 alpha-2 code.")
            .RequirePermission(ReferenceDataPermissions.ReadLists)
            .WithReferenceDataCaching(ReferenceListCaching.MaxAge);
    }

    private static async Task<Results<Ok<CountriesResponse>, ProblemHttpResult>> ListAsync(
        IQueryHandler<ListCountriesQuery, IReadOnlyList<CountryEntry>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListCountriesQuery(), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(new CountriesResponse([.. result.Value.Select(country => new CountryResponse(country.Code, country.Name))]))
            : result.Error!.ToProblem();
    }
}
