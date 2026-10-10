using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.AdmitPerson;

// The person's own record is the one linked to their Entra account; before their first sign-in that can only be a record an
// administrator registered with their work email and no Entra account yet. A deleted record is no record, so a person who
// still holds an app role of the registration starts a new one.
internal sealed class AdmitPersonHandler(UsersDbContext context, TimeProvider timeProvider) : ICommandHandler<AdmitPersonCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(AdmitPersonCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var signedInAt = timeProvider.GetUtcNow();
        var workEmail = User.NormaliseWorkEmail(command.WorkEmail);
        var user = await context.Users.SingleOrDefaultAsync(u => u.EntraObjectId == command.EntraObjectId, cancellationToken).ConfigureAwait(false)
            ?? await context.Users.SingleOrDefaultAsync(u => u.EntraObjectId == null && u.WorkEmail == workEmail, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            var created = User.FirstSignIn(command.EntraObjectId, command.DisplayName, workEmail, signedInAt);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            context.Users.Add(created.Value);

            return true;
        }

        if (!user.IsActive)
        {
            return false;
        }

        var signedIn = user.SignIn(command.EntraObjectId, command.DisplayName, workEmail, signedInAt);
        if (signedIn.IsFailure)
        {
            return signedIn.Error!;
        }

        return true;
    }
}
