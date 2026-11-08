using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Persistence;

// The context that owns the numbering tables, so its migrations create them, where every other context that maps them leaves
// them out of its own.
internal sealed class NumberingDbContext(DbContextOptions<NumberingDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = NumberingTables.Schema;

    public DbSet<NumberSeries> Series => Set<NumberSeries>();

    public DbSet<NumberSeriesYear> Years => Set<NumberSeriesYear>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NumberSeries>().ToTable(NumberingTables.Series, NumberingTables.Schema, table => table.ExcludeFromMigrations(false));
        modelBuilder.Entity<NumberSeriesYear>().ToTable(NumberingTables.Years, NumberingTables.Schema, table => table.ExcludeFromMigrations(false));
    }
}
