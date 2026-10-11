using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application;

// A position holds from its own date until the date the person's next position starts.
internal static class PositionHistory
{
    public static Task<DateOnly?> NextStartAsync(UsersDbContext context, UserId person, DateOnly after, CancellationToken cancellationToken) =>
        context.Positions
            .Where(p => p.UserId == person && p.EffectiveFrom > after)
            .OrderBy(p => p.EffectiveFrom)
            .Select(p => (DateOnly?)p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

    public static bool HoldsOnOrAfter(DateOnly? until, DateOnly date) => until is not { } end || end > date;

    public static async Task<List<ReportingLine>> ReportingLinesAsync(UsersDbContext context, CancellationToken cancellationToken) =>
        await context.Positions
            .AsNoTracking()
            .Select(p => new ReportingLine(p.UserId, p.EffectiveFrom, p.ManagerId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
