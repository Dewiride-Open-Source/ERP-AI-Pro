using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Shared.Positions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application.Commands.DeleteDepartment;

internal sealed class DeleteDepartmentHandler(UsersDbContext context, TimeProvider timeProvider) : ICommandHandler<DeleteDepartmentCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(DeleteDepartmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await OrganisationLock.TryAcquireAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return OrganisationLock.Busy;
        }

        var id = DepartmentId.From(command.Id);
        var department = await context.Departments.SingleOrDefaultAsync(d => d.Id == id, cancellationToken).ConfigureAwait(false);
        if (department is null)
        {
            return DepartmentErrors.NotFound;
        }

        var today = IndianStandardTime.Today(timeProvider);
        if (await context.Departments.AnyAsync(d => d.ParentId == id, cancellationToken).ConfigureAwait(false)
            || await CountingPositions.On(context, today).AnyAsync(p => p.DepartmentId == id, cancellationToken).ConfigureAwait(false))
        {
            return DepartmentErrors.InUse;
        }

        context.Departments.Remove(department);

        return department.Id.Value;
    }
}
