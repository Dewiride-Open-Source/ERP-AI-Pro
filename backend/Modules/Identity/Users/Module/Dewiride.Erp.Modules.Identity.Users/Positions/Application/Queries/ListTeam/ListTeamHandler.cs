using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Shared.Positions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.ListTeam;

// The team a deactivation hands over, so the people it lists are the ones whose positions the handover moves.
internal sealed class ListTeamHandler(UsersDbContext context, TimeProvider timeProvider) : IQueryHandler<ListTeamQuery, IReadOnlyList<TeamMemberDetails>>
{
    public async Task<Result<IReadOnlyList<TeamMemberDetails>>> HandleAsync(ListTeamQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var person = UserId.From(query.PersonId);
        if (!await context.Users.AnyAsync(u => u.Id == person, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.NotFound;
        }

        var members = TeamPositions.Of(context, person, IndianStandardTime.Today(timeProvider)).Select(position => position.UserId);
        var team = await context.Users
            .AsNoTracking()
            .Where(u => members.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Id)
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return team.ConvertAll(member => new TeamMemberDetails(member.Id.Value, member.DisplayName));
    }
}
