using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Queries;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application.Queries.ListDepartments;

internal sealed class ListDepartmentsHandler(UsersDbContext context) : IQueryHandler<ListDepartmentsQuery, PagedResult<DepartmentDetails>>
{
    private static readonly SortableFields<Department> Sortable = new SortableFields<Department>()
        .Add("name", d => d.Name)
        .Add("createdAt", d => d.CreatedAt);

    private static readonly FilterableFields<Department> Filterable = new FilterableFields<Department>()
        .Add("name", d => d.Name)
        .Add("parentId", d => d.ParentId);

    private static readonly SortRequest ByName = new([new SortTerm("name", SortDirection.Ascending)]);

    public async Task<Result<PagedResult<DepartmentDetails>>> HandleAsync(ListDepartmentsQuery query, CancellationToken cancellationToken)
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

        var page = await context.Departments
            .AsNoTracking()
            .ApplyFilter(filter.Value)
            .ApplySort(sort.Value, d => d.Id)
            .ToPagedResultAsync(query.Request.Page, cancellationToken)
            .ConfigureAwait(false);

        return page.Map(DepartmentDetails.From);
    }
}
