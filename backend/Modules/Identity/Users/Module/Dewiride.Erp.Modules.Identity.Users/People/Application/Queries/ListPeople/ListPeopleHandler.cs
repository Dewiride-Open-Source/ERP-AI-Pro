using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Persistence.Queries;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.ListPeople;

// The department, designation and manager a person is sorted and filtered by are those of the position they hold today.
internal sealed class ListPeopleHandler(UsersDbContext context, TimeProvider timeProvider) : IQueryHandler<ListPeopleQuery, PagedResult<PersonDetails>>
{
    private static readonly SortableFields<PersonRow> Sortable = new SortableFields<PersonRow>()
        .Add("displayName", r => r.User.DisplayName)
        .Add("workEmail", r => r.User.WorkEmail)
        .Add("employeeCode", r => r.User.EmployeeCode)
        .Add("department", r => r.DepartmentName)
        .Add("designation", r => r.DesignationName)
        .Add("manager", r => r.ManagerDisplayName)
        .Add("dateOfJoining", r => r.User.DateOfJoining)
        .Add("status", r => r.User.Status)
        .Add("lastSignedInAt", r => r.User.LastSignedInAt)
        .Add("createdAt", r => r.User.CreatedAt);

    private static readonly FilterableFields<PersonRow> Filterable = new FilterableFields<PersonRow>()
        .Add("displayName", r => r.User.DisplayName)
        .Add("workEmail", r => r.User.WorkEmail)
        .Add("employeeCode", r => r.User.EmployeeCode)
        .Add("departmentId", r => r.DepartmentId)
        .Add("designationId", r => r.DesignationId)
        .Add("managerId", r => r.ManagerId)
        .Add("dateOfJoining", r => r.User.DateOfJoining)
        .Add("status", r => r.User.Status);

    private static readonly SortRequest ByName = new([new SortTerm("displayName", SortDirection.Ascending)]);

    public async Task<Result<PagedResult<PersonDetails>>> HandleAsync(ListPeopleQuery query, CancellationToken cancellationToken)
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

        var page = await PeopleWithPositions.On(context, IndianStandardTime.Today(timeProvider))
            .AsNoTracking()
            .ApplyFilter(filter.Value)
            .ApplySort(sort.Value, r => r.User.Id)
            .ToPagedResultAsync(query.Request.Page, cancellationToken)
            .ConfigureAwait(false);

        return page.Map(PersonDetails.From);
    }
}
