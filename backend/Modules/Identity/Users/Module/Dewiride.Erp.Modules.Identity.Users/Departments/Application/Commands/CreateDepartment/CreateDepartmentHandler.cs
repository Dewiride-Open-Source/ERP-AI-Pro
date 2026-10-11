using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application.Commands.CreateDepartment;

internal sealed class CreateDepartmentHandler(UsersDbContext context) : ICommandHandler<CreateDepartmentCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var parentId = command.ParentId is { } parent ? DepartmentId.From(parent) : (DepartmentId?)null;
        var created = Department.Create(command.Name, parentId);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        if (!await OrganisationLock.TryAcquireAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return OrganisationLock.Busy;
        }

        if (parentId is { } id && !await context.Departments.AnyAsync(d => d.Id == id, cancellationToken).ConfigureAwait(false))
        {
            return DepartmentErrors.ParentNotFound;
        }

        var department = created.Value;
        if (await context.Departments.AnyAsync(d => d.ParentId == parentId && d.Name == department.Name, cancellationToken).ConfigureAwait(false))
        {
            return DepartmentErrors.NameTaken;
        }

        context.Departments.Add(department);

        return department.Id.Value;
    }
}
