using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Application.Queries.ListRecentStartups;

internal sealed record ListRecentStartupsQuery(int Count = ListRecentStartupsQuery.DefaultCount) : IQuery<IReadOnlyList<StartupDetails>>
{
    public const int MinimumCount = 1;

    public const int MaximumCount = 100;

    public const int DefaultCount = 20;
}
