using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.UpdatePerson;

// The version the edit was made from becomes the original rowversion, so saving refuses an edit of a record that changed
// since it was read and the unit of work answers it as a conflict.
internal sealed class UpdatePersonHandler(UsersDbContext context) : ICommandHandler<UpdatePersonCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(UpdatePersonCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!PersonVersion.TryDecode(command.Version, out var version))
        {
            return UserErrors.VersionInvalid;
        }

        var id = UserId.From(command.Id);
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        context.Entry(user).Property(u => u.RowVersion).OriginalValue = version;
        var described = user.Describe(command.EmployeeCode, command.PhoneNumber, command.Designation, command.DateOfJoining);
        if (described.IsFailure)
        {
            return described.Error!;
        }

        if (user.EmployeeCode is { } code && await context.Users.AnyAsync(u => u.Id != id && u.EmployeeCode == code, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.EmployeeCodeTaken;
        }

        return user.Id.Value;
    }
}
