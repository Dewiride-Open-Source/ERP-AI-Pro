using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Shared.Positions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application.Commands.DeleteDesignation;

internal sealed class DeleteDesignationHandler(UsersDbContext context, TimeProvider timeProvider) : ICommandHandler<DeleteDesignationCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(DeleteDesignationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await OrganisationLock.TryAcquireAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return OrganisationLock.Busy;
        }

        var id = DesignationId.From(command.Id);
        var designation = await context.Designations.SingleOrDefaultAsync(d => d.Id == id, cancellationToken).ConfigureAwait(false);
        if (designation is null)
        {
            return DesignationErrors.NotFound;
        }

        var today = IndianStandardTime.Today(timeProvider);
        if (await CountingPositions.On(context, today).AnyAsync(p => p.DesignationId == id, cancellationToken).ConfigureAwait(false))
        {
            return DesignationErrors.InUse;
        }

        context.Designations.Remove(designation);

        return designation.Id.Value;
    }
}
