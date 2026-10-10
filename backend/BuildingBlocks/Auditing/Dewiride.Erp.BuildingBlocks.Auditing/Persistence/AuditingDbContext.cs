using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Persistence;

internal sealed class AuditingDbContext(DbContextOptions<AuditingDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "audit";

    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
}
