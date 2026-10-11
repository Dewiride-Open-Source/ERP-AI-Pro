using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application.Commands.CreateDesignation;

internal sealed class CreateDesignationHandler(UsersDbContext context) : ICommandHandler<CreateDesignationCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateDesignationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = Designation.Create(command.Name);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var designation = created.Value;
        if (await context.Designations.AnyAsync(d => d.Name == designation.Name, cancellationToken).ConfigureAwait(false))
        {
            return DesignationErrors.NameTaken;
        }

        context.Designations.Add(designation);

        return designation.Id.Value;
    }
}
