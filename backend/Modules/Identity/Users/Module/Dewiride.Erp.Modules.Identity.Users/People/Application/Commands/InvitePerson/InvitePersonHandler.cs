using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.InvitePerson;

// The invited record is linked to the person's Entra account from the start, so their first sign-in finds it by object id.
// Only a record linked to no Entra account holds a work email a sign-in still matches, so only such a record makes the
// directory's sign-in name a duplicate; a linked record with the same email kept an address Entra has since given this
// person, as the sign-in itself allows.
internal sealed class InvitePersonHandler(UsersDbContext context) : ICommandHandler<InvitePersonCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(InvitePersonCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invited = User.Invite(
            command.Person.ObjectId,
            command.Person.DisplayName,
            command.Person.UserPrincipalName,
            command.EmployeeCode,
            command.PhoneNumber,
            command.Designation,
            command.DateOfJoining);
        if (invited.IsFailure)
        {
            return invited.Error!;
        }

        var user = invited.Value;
        if (await context.Users.AnyAsync(u => u.EntraObjectId == user.EntraObjectId, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.EntraObjectIdTaken;
        }

        if (await context.Users.AnyAsync(u => u.EntraObjectId == null && u.WorkEmail == user.WorkEmail, cancellationToken).ConfigureAwait(false))
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
