using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.Modules.Identity.Users.Contracts.Positions;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;
using Dewiride.Erp.Modules.Identity.Users.Shared.Positions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.PublicApi;

// The walk stops at a person it has met already, the person asked about included, so reporting lines written outside the API that
// loop still end and never list the person among their own managers. A position keeps naming a manager whose record has been
// deactivated or deleted since, so the managers are read deleted ones included, each active only while their record is neither.
internal sealed class ReportingLineDirectory(UsersDbContext context) : IReportingLines
{
    public async Task<IReadOnlyList<ReportingManagerEntry>> ManagersOfAsync(Guid objectId, DateOnly on, CancellationToken cancellationToken)
    {
        var person = await context.Users
            .AsNoTracking()
            .Where(u => u.EntraObjectId == objectId)
            .Select(u => new { u.Id })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (person is null)
        {
            return [];
        }

        var lines = new ReportingLines(await PositionHistory.ReportingLinesAsync(context, cancellationToken).ConfigureAwait(false));
        var seen = new HashSet<UserId> { person.Id };
        var chain = new List<UserId>();
        for (var manager = lines.ManagerOf(person.Id, on); manager is { } id && seen.Add(id); manager = lines.ManagerOf(id, on))
        {
            chain.Add(id);
        }

        if (chain.Count == 0)
        {
            return [];
        }

        var managers = await context.Users
            .IncludeDeleted()
            .AsNoTracking()
            .Where(u => chain.Contains(u.Id))
            .Select(u => new { u.Id, u.EntraObjectId, IsActive = u.Status == UserStatus.Active && !u.IsDeleted })
            .ToDictionaryAsync(u => u.Id, u => new ReportingManagerEntry(u.Id.Value, u.EntraObjectId, u.IsActive), cancellationToken)
            .ConfigureAwait(false);

        return [.. chain.Select(manager => managers[manager])];
    }
}
