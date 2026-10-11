using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Shared.Versioning;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application.Commands.UpdateDesignation;

// A new name holds for every position that names the designation, past ones included.
internal sealed class UpdateDesignationHandler(UsersDbContext context) : ICommandHandler<UpdateDesignationCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(UpdateDesignationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!RecordVersion.TryDecode(command.Version, out var version))
        {
            return DesignationErrors.VersionInvalid;
        }

        var id = DesignationId.From(command.Id);
        var designation = await context.Designations.SingleOrDefaultAsync(d => d.Id == id, cancellationToken).ConfigureAwait(false);
        if (designation is null)
        {
            return DesignationErrors.NotFound;
        }

        context.Entry(designation).Property(d => d.RowVersion).OriginalValue = version;
        var renamed = designation.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return renamed.Error!;
        }

        if (await context.Designations.AnyAsync(d => d.Id != id && d.Name == designation.Name, cancellationToken).ConfigureAwait(false))
        {
            return DesignationErrors.NameTaken;
        }

        return designation.Id.Value;
    }
}
