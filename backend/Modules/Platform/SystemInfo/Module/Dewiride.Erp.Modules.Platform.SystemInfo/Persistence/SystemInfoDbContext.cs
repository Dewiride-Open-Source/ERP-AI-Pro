using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Persistence;

internal sealed class SystemInfoDbContext(DbContextOptions<SystemInfoDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "platform_system_info";

    public DbSet<ApiStartup> Startups => Set<ApiStartup>();
}
