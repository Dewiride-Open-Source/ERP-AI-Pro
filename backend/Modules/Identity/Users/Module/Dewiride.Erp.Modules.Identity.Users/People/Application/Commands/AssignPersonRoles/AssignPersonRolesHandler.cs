using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Shared.Versioning;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.AssignPersonRoles;

// The version the change was made from becomes the original rowversion, so saving refuses a change of a record that changed
// since it was read. The roles replace the record's own, and the person's next request holds them.
internal sealed class AssignPersonRolesHandler(UsersDbContext context) : ICommandHandler<AssignPersonRolesCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(AssignPersonRolesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!RecordVersion.TryDecode(command.Version, out var version))
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
        var assigned = user.AssignRoles(PersonRoles.ToErpRoles(command.Roles));

        return assigned.IsFailure ? assigned.Error! : user.Id.Value;
    }
}
