using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Caching.Persistence;

internal sealed class CachingDbContext(DbContextOptions<CachingDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "platform_caching";

    public const string DistributedCacheTable = "DistributedCacheEntries";
}
