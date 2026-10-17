using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Persistence;

// Owns the audit schema, so its migrations create the change records every other context maps excluded from theirs, and the
// role whose members may insert into the schema but never update, delete or alter what it holds.
internal sealed class AuditingDbContext(DbContextOptions<AuditingDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = ChangeRecordMapping.Schema;

    public const string AppendOnlyRole = "erp_audit_append_only";

    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    public DbSet<ChangeRecord> ChangeRecords => Set<ChangeRecord>();
}
