using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;
using Dewiride.Erp.Modules.Identity.Users.Shared.Positions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Commands.WithdrawPosition;

// Withdrawing a position hands its dates to the person's position before it. When that one then holds today or later it is
// checked as a new one would be, because what it names may have been deleted or deactivated since it ended.
internal sealed class WithdrawPositionHandler(UsersDbContext context, TimeProvider timeProvider) : ICommandHandler<WithdrawPositionCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(WithdrawPositionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await OrganisationLock.TryAcquireAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return OrganisationLock.Busy;
        }

        var person = UserId.From(command.PersonId);
        if (!await context.Users.AnyAsync(u => u.Id == person, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.NotFound;
        }

        var id = PositionId.From(command.PositionId);
        var position = await context.Positions.SingleOrDefaultAsync(p => p.Id == id && p.UserId == person, cancellationToken).ConfigureAwait(false);
        if (position is null)
        {
            return PositionErrors.NotFound;
        }

        var previous = await context.Positions
            .AsNoTracking()
            .Where(p => p.UserId == person && p.EffectiveFrom < position.EffectiveFrom)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var until = await PositionHistory.NextStartAsync(context, person, position.EffectiveFrom, cancellationToken).ConfigureAwait(false);
        if (previous is not null)
        {
            if (PositionHistory.HoldsOnOrAfter(until, IndianStandardTime.Today(timeProvider)) && !await StillValidAsync(previous, cancellationToken).ConfigureAwait(false))
            {
                return PositionErrors.PreviousNoLongerValid;
            }

            var lines = await PositionHistory.ReportingLinesAsync(context, cancellationToken).ConfigureAwait(false);
            lines.RemoveAll(line => line.Person == person && line.EffectiveFrom == position.EffectiveFrom);
            if (previous.ManagerId is not null && new ReportingLines(lines).ClosesCycle(person, position.EffectiveFrom, until))
            {
                return PositionErrors.ReportingCycle;
            }
        }

        context.Positions.Remove(position);

        return position.Id.Value;
    }

    private async Task<bool> StillValidAsync(Position previous, CancellationToken cancellationToken)
    {
        if (previous.DepartmentId is { } department && !await context.Departments.AnyAsync(d => d.Id == department, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (previous.DesignationId is { } designation && !await context.Designations.AnyAsync(d => d.Id == designation, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return previous.ManagerId is not { } manager
            || await context.Users.AnyAsync(u => u.Id == manager && u.Status == UserStatus.Active, cancellationToken).ConfigureAwait(false);
    }
}
