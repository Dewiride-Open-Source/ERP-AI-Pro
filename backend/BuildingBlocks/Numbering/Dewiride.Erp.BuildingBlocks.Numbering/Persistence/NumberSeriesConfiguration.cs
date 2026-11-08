using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Persistence;

// Every context that numbers its documents maps this table, so its numbers commit with its documents; the table belongs to
// NumberingDbContext, so the mapping leaves it out of every other context's migrations.
internal sealed class NumberSeriesConfiguration : IEntityTypeConfiguration<NumberSeries>
{
    public void Configure(EntityTypeBuilder<NumberSeries> builder)
    {
        builder.ToTable(NumberingTables.Series, NumberingTables.Schema, table => table.ExcludeFromMigrations());
        builder.RecordChangesAs(NumberingTables.SeriesChangeKey);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.DocumentType).HasMaxLength(NumberSeries.DocumentTypeMaxLength).IsUnicode(false);
        builder.ComplexProperty(s => s.Format, NumberFormatMapping.Configure);
        builder.HasIndex(s => s.DocumentType).IsUnique();
    }
}
