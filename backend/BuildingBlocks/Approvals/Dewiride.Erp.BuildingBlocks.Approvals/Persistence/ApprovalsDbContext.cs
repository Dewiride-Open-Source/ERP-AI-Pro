using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Persistence;

// The context that owns the approval tables, so its migrations create the requests and steps that every other context mapping them
// leaves out of its own, and the kinds' settings, which no other context maps. It reads the requests across every kind for the lists
// of the Approvals page.
internal sealed class ApprovalsDbContext(DbContextOptions<ApprovalsDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = ApprovalTables.Schema;

    public DbSet<ApprovalRequest> Requests => Set<ApprovalRequest>();

    public DbSet<ApprovalStep> Steps => Set<ApprovalStep>();

    public DbSet<ApprovalKindSetting> KindSettings => Set<ApprovalKindSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApprovalRequest>().ToTable(ApprovalTables.Requests, ApprovalTables.Schema, table => table.ExcludeFromMigrations(false));
        modelBuilder.Entity<ApprovalStep>().ToTable(ApprovalTables.Steps, ApprovalTables.Schema, table => table.ExcludeFromMigrations(false));
    }
}
