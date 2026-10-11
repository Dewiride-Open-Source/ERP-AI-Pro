using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application.Queries.GetDepartment;

internal sealed class GetDepartmentHandler(UsersDbContext context) : IQueryHandler<GetDepartmentQuery, DepartmentDetails>
{
    public async Task<Result<DepartmentDetails>> HandleAsync(GetDepartmentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = DepartmentId.From(query.Id);
        var department = await context.Departments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, cancellationToken).ConfigureAwait(false);

        return department is null ? DepartmentErrors.NotFound : DepartmentDetails.From(department);
    }
}
