using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Platform.SystemInfo.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Application.Queries.ListRecentStartups;

internal sealed class ListRecentStartupsHandler(SystemInfoDbContext context) : IQueryHandler<ListRecentStartupsQuery, IReadOnlyList<StartupDetails>>
{
    public async Task<Result<IReadOnlyList<StartupDetails>>> HandleAsync(ListRecentStartupsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var startups = await context.Startups
            .AsNoTracking()
            .OrderByDescending(s => s.StartedAt)
            .ThenByDescending(s => s.RecordedAt)
            .Take(query.Count)
            .Select(s => new StartupDetails(
                s.Id.Value,
                s.ApplicationName,
                s.Build.Version,
                s.Build.Framework,
                s.EnvironmentName,
                s.ConfigurationLabel,
                s.MachineName,
                s.StartedAt,
                s.RecordedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<StartupDetails>>(startups);
    }
}
