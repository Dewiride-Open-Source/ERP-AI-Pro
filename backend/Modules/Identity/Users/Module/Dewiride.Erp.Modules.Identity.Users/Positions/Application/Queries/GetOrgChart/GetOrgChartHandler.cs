using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Queries;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Shared.Positions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.GetOrgChart;

// The chart holds the people whose record is active now and who have joined by the date, with the position each holds on it,
// so a planned hire appears from their date of joining. A person's status is not dated, so the chart of a past date leaves out
// someone deactivated since, and their reports name a manager outside the chart.
internal sealed class GetOrgChartHandler(UsersDbContext context) : IQueryHandler<GetOrgChartQuery, PagedResult<OrgChartPersonDetails>>
{
    private static readonly SortableFields<PersonRow> Sortable = new SortableFields<PersonRow>()
        .Add("displayName", r => r.User.DisplayName)
        .Add("department", r => r.DepartmentName);

    private static readonly FilterableFields<PersonRow> Filterable = new FilterableFields<PersonRow>()
        .Add("departmentId", r => r.DepartmentId)
        .Add("managerId", r => r.ManagerId);

    private static readonly SortRequest ByName = new([new SortTerm("displayName", SortDirection.Ascending)]);

    public async Task<Result<PagedResult<OrgChartPersonDetails>>> HandleAsync(GetOrgChartQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sort = (query.Request.Sort.Terms.Count == 0 ? ByName : query.Request.Sort).Resolve(Sortable);
        if (sort.IsFailure)
        {
            return sort.Error!;
        }

        var filter = query.Request.Filter.Resolve(Filterable);
        if (filter.IsFailure)
        {
            return filter.Error!;
        }

        var page = await PeopleWithPositions.On(context, query.On)
            .Where(r => r.User.Status == UserStatus.Active && (r.User.DateOfJoining == null || r.User.DateOfJoining <= query.On))
            .AsNoTracking()
            .ApplyFilter(filter.Value)
            .ApplySort(sort.Value, r => r.User.Id)
            .ToPagedResultAsync(query.Request.Page, cancellationToken)
            .ConfigureAwait(false);

        return page.Map(OrgChartPersonDetails.From);
    }
}
