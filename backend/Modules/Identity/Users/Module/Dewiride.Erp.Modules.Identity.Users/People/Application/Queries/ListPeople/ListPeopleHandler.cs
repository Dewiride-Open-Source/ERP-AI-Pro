using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Queries;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.ListPeople;

internal sealed class ListPeopleHandler(UsersDbContext context) : IQueryHandler<ListPeopleQuery, PagedResult<PersonDetails>>
{
    private static readonly SortableFields<User> Sortable = new SortableFields<User>()
        .Add("displayName", u => u.DisplayName)
        .Add("workEmail", u => u.WorkEmail)
        .Add("employeeCode", u => u.EmployeeCode)
        .Add("designation", u => u.Designation)
        .Add("dateOfJoining", u => u.DateOfJoining)
        .Add("status", u => u.Status)
        .Add("lastSignedInAt", u => u.LastSignedInAt)
        .Add("createdAt", u => u.CreatedAt);

    private static readonly FilterableFields<User> Filterable = new FilterableFields<User>()
        .Add("displayName", u => u.DisplayName)
        .Add("workEmail", u => u.WorkEmail)
        .Add("employeeCode", u => u.EmployeeCode)
        .Add("designation", u => u.Designation)
        .Add("dateOfJoining", u => u.DateOfJoining)
        .Add("status", u => u.Status);

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

        var page = await context.Users
            .AsNoTracking()
            .ApplyFilter(filter.Value)
            .ApplySort(sort.Value, u => u.Id)
            .ToPagedResultAsync(query.Request.Page, cancellationToken)
            .ConfigureAwait(false);

        return page.Map(PersonDetails.From);
    }
}
