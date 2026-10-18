using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;
using Dewiride.Erp.Modules.Identity.Users.Shared.Positions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.ChangePersonStatus;

// Positions are never edited, so the team moves the way an administrator would move each member: the position a member holds
// today is followed by one from today with its department and designation and the new manager, or replaced on its own date
// when it starts today, withdrawn and recorded again as WithdrawPosition and RecordPosition would; and each of the member's
// later positions that names the person is replaced on its own date the same way. Every rule is checked before anything
// changes, the reporting-cycle rule over the positions as they would then stand.
internal sealed class TeamHandoverPlan
{
    private readonly IReadOnlyList<Position> _withdrawn;

    private readonly IReadOnlyList<Position> _recorded;

    private TeamHandoverPlan(IReadOnlyList<Position> withdrawn, IReadOnlyList<Position> recorded)
    {
        _withdrawn = withdrawn;
        _recorded = recorded;
    }

    public static async Task<Result<TeamHandoverPlan>> PlanAsync(UsersDbContext context, UserId person, TeamHandover? handover, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var team = await TeamPositions.Of(context, person, today).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (team.Count == 0)
        {
            return new TeamHandoverPlan([], []);
        }

        if (handover is null)
        {
            return UserErrors.TeamHandoverRequired;
        }

        var manager = handover.ManagerId is { } managerId ? UserId.From(managerId) : (UserId?)null;
        if (manager is { } newManager && await RefusalOfAsync(context, person, newManager, team, cancellationToken).ConfigureAwait(false) is { } refusal)
        {
            return refusal;
        }

        var recorded = new List<Position>(team.Count);
        foreach (var position in team)
        {
            var moved = Position.Record(position.UserId, position.EffectiveFrom > today ? position.EffectiveFrom : today, position.DepartmentId, position.DesignationId, manager);
            if (moved.IsFailure)
            {
                return moved.Error!;
            }

            recorded.Add(moved.Value);
        }

        var withdrawn = team.Where(position => position.EffectiveFrom >= today).ToList();
        if (manager is not null && await ClosesCycleAsync(context, withdrawn, recorded, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.TeamReportingCycle;
        }

        return new TeamHandoverPlan(withdrawn, recorded);
    }

    public void Apply(UsersDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Positions.RemoveRange(_withdrawn);
        context.Positions.AddRange(_recorded);
    }

    private static async Task<Error?> RefusalOfAsync(UsersDbContext context, UserId person, UserId manager, List<Position> team, CancellationToken cancellationToken)
    {
        if (manager == person)
        {
            return UserErrors.TeamManagerSamePerson;
        }

        var status = await context.Users.Where(u => u.Id == manager).Select(u => (UserStatus?)u.Status).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (status is null)
        {
            return UserErrors.TeamManagerNotFound;
        }

        if (status != UserStatus.Active)
        {
            return UserErrors.TeamManagerInactive;
        }

        return team.Exists(position => position.UserId == manager) ? UserErrors.TeamManagerInTeam : null;
    }

    private static async Task<bool> ClosesCycleAsync(UsersDbContext context, List<Position> withdrawn, List<Position> recorded, CancellationToken cancellationToken)
    {
        var lines = await PositionHistory.ReportingLinesAsync(context, cancellationToken).ConfigureAwait(false);
        lines.RemoveAll(line => withdrawn.Exists(position => position.UserId == line.Person && position.EffectiveFrom == line.EffectiveFrom));
        lines.AddRange(recorded.Select(position => new ReportingLine(position.UserId, position.EffectiveFrom, position.ManagerId)));
        var reporting = new ReportingLines(lines);

        return recorded.Exists(position => reporting.ClosesCycle(position.UserId, position.EffectiveFrom, NextStart(lines, position.UserId, position.EffectiveFrom)));
    }

    private static DateOnly? NextStart(List<ReportingLine> lines, UserId person, DateOnly after) =>
        lines.Where(line => line.Person == person && line.EffectiveFrom > after).Select(line => (DateOnly?)line.EffectiveFrom).Min();
}
