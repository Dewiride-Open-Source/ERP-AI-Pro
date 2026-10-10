using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.RegisterPerson;

internal sealed class RegisterPersonHandler(UsersDbContext context) : ICommandHandler<RegisterPersonCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RegisterPersonCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var registered = User.Register(
            command.EntraObjectId,
            command.DisplayName,
            command.WorkEmail,
            command.EmployeeCode,
            command.PhoneNumber,
            command.Designation,
            command.DateOfJoining);
        if (registered.IsFailure)
        {
            return registered.Error!;
        }

        var user = registered.Value;
        if (user.EntraObjectId is { } objectId && await context.Users.AnyAsync(u => u.EntraObjectId == objectId, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.EntraObjectIdTaken;
        }

        if (await context.Users.AnyAsync(u => u.WorkEmail == user.WorkEmail, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.WorkEmailTaken;
        }

        if (user.EmployeeCode is { } code && await context.Users.AnyAsync(u => u.EmployeeCode == code, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.EmployeeCodeTaken;
        }

        context.Users.Add(user);

        return user.Id.Value;
    }
}
