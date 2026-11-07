using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Caching;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts;
using Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Application.Queries.ListFinancialYears;
using Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Endpoints.Requests;
using Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.ReferenceData.Shared.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Endpoints;

internal static class FinancialYearEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/financial-years", ListAsync)
            .WithName("Platform.ReferenceData.ListFinancialYears")
            .WithSummary("Lists the financial years from one to another, each with its quarters and its twelve monthly periods from April.")
            .RequirePermission(ReferenceDataPermissions.ReadLists)
            .WithReferenceDataCaching(ReferenceListCaching.MaxAge);
    }

    private static FinancialYearResponse ToResponse(FinancialYear year) =>
        new(
            year.Label,
            year.Start,
            year.End,
            [.. year.Quarters.Select(quarter => new FinancialQuarterResponse(quarter.Number, quarter.Label, quarter.Start, quarter.End))],
            [.. year.Periods.Select(period => new FinancialPeriodResponse(period.Number, period.Label, period.Quarter.Number, period.Start, period.End))]);

    private static async Task<Results<Ok<FinancialYearsResponse>, ProblemHttpResult>> ListAsync(
        [AsParameters] ListFinancialYearsRequest request,
        IQueryHandler<ListFinancialYearsQuery, IReadOnlyList<FinancialYear>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListFinancialYearsQuery(request.From!, request.To!), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(new FinancialYearsResponse([.. result.Value.Select(ToResponse)]))
            : result.Error!.ToProblem();
    }
}
