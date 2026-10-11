using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Shared;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application.Commands.UpdateDepartment;

// The version the change was made from becomes the original rowversion, so saving refuses a change to a department that
// changed since it was read.
internal sealed class UpdateDepartmentHandler(UsersDbContext context) : ICommandHandler<UpdateDepartmentCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(UpdateDepartmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!RecordVersion.TryDecode(command.Version, out var version))
        {
            return DepartmentErrors.VersionInvalid;
        }

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

        context.Entry(department).Property(d => d.RowVersion).OriginalValue = version;
        var renamed = department.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return renamed.Error!;
        }

        var parentId = command.ParentId is { } parent ? DepartmentId.From(parent) : (DepartmentId?)null;
        if (parentId is { } parentToCheck && !await context.Departments.AnyAsync(d => d.Id == parentToCheck, cancellationToken).ConfigureAwait(false))
        {
            return DepartmentErrors.ParentNotFound;
        }

        var parents = await context.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.ParentId, cancellationToken).ConfigureAwait(false);
        var moved = department.MoveUnder(parentId, new DepartmentTree(parents));
        if (moved.IsFailure)
        {
            return moved.Error!;
        }

        if (await context.Departments.AnyAsync(d => d.Id != id && d.ParentId == parentId && d.Name == department.Name, cancellationToken).ConfigureAwait(false))
        {
            return DepartmentErrors.NameTaken;
        }

        return department.Id.Value;
    }
}
