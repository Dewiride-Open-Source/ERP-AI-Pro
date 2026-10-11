using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Commands.RecordPosition;

// A deactivated manager can still be named by a position that ended before today, because they were active then; a deleted
// one never, because a deleted record was a mistake rather than someone who left.
internal sealed class RecordPositionHandler(UsersDbContext context, TimeProvider timeProvider) : ICommandHandler<RecordPositionCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RecordPositionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var person = UserId.From(command.PersonId);
        var departmentId = command.DepartmentId is { } department ? DepartmentId.From(department) : (DepartmentId?)null;
        var designationId = command.DesignationId is { } designation ? DesignationId.From(designation) : (DesignationId?)null;
        var managerId = command.ManagerId is { } manager ? UserId.From(manager) : (UserId?)null;
        var recorded = Position.Record(person, command.EffectiveFrom, departmentId, designationId, managerId);
        if (recorded.IsFailure)
        {
            return recorded.Error!;
        }

        if (!await OrganisationLock.TryAcquireAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return OrganisationLock.Busy;
        }

        if (!await context.Users.AnyAsync(u => u.Id == person, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.NotFound;
        }

        if (await context.Positions.AnyAsync(p => p.UserId == person && p.EffectiveFrom == command.EffectiveFrom, cancellationToken).ConfigureAwait(false))
        {
            return PositionErrors.DateTaken;
        }

        if (departmentId is { } departmentToCheck && !await context.Departments.AnyAsync(d => d.Id == departmentToCheck, cancellationToken).ConfigureAwait(false))
        {
            return PositionErrors.DepartmentNotFound;
        }

        if (designationId is { } designationToCheck && !await context.Designations.AnyAsync(d => d.Id == designationToCheck, cancellationToken).ConfigureAwait(false))
        {
            return PositionErrors.DesignationNotFound;
        }

        var until = await PositionHistory.NextStartAsync(context, person, command.EffectiveFrom, cancellationToken).ConfigureAwait(false);
        if (managerId is { } managerToCheck)
        {
            var status = await context.Users.Where(u => u.Id == managerToCheck).Select(u => (UserStatus?)u.Status).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (status is null)
            {
                return PositionErrors.ManagerNotFound;
            }

            if (status != UserStatus.Active && PositionHistory.HoldsOnOrAfter(until, IndianStandardTime.Today(timeProvider)))
            {
                return PositionErrors.ManagerInactive;
            }

            var lines = await PositionHistory.ReportingLinesAsync(context, cancellationToken).ConfigureAwait(false);
            lines.Add(new ReportingLine(person, command.EffectiveFrom, managerId));
            if (new ReportingLines(lines).ClosesCycle(person, command.EffectiveFrom, until))
            {
                return PositionErrors.ReportingCycle;
            }
        }

        context.Positions.Add(recorded.Value);

        return recorded.Value.Id.Value;
    }
}
