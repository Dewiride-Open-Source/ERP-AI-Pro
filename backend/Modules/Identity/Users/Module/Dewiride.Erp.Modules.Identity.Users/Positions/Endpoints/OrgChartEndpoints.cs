using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.GetOrgChart;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Requests;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints;

// Anyone signed in reads today's chart; another date's would show where people were or will be, which is the history of
// their positions, so it needs the permission to read that history.
internal static class OrgChartEndpoints
{
    public static readonly Error OtherDateForbidden = Error.Forbidden("request.forbidden", $"The chart of a date other than today needs the permission {UsersPermissions.ReadPositions}.");

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/org-chart", GetAsync)
            .WithName("Identity.Users.OrgChart")
            .WithSummary("Lists the active people with the department, designation and manager of the position each holds on a date, today unless the caller names another.")
            .RequirePermission(UsersPermissions.ReadOrgChart);
    }

    private static OrgChartPersonResponse ToResponse(OrgChartPersonDetails details) =>
        new(
            details.Id,
            details.DisplayName,
            details.DepartmentId,
            details.DepartmentName,
            details.DesignationId,
            details.DesignationName,
            details.ManagerId,
            details.ManagerDisplayName);

    private static async Task<Results<Ok<PagedResponse<OrgChartPersonResponse>>, ProblemHttpResult>> GetAsync(
        [AsParameters] OrgChartRequest request,
        [AsParameters] PagingParameters paging,
        IQueryHandler<GetOrgChartQuery, PagedResult<OrgChartPersonDetails>> handler,
        ICallerPermissions permissions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var today = IndianStandardTime.Today(timeProvider);
        var on = request.On ?? today;
        if (on != today && !permissions.Holds(UsersPermissions.ReadPositions))
        {
            return OtherDateForbidden.ToProblem();
        }

        var list = paging.ToListRequest();
        if (list.IsFailure)
        {
            return list.Error!.ToProblem();
        }

        var result = await handler.HandleAsync(new GetOrgChartQuery(on, list.Value), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value.ToResponse(ToResponse)) : result.Error!.ToProblem();
    }
}
