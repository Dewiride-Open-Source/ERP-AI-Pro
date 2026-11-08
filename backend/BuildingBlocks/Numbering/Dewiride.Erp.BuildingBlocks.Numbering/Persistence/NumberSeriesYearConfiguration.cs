using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Persistence;

// The year is stored as its label, so the audit trail shows 2026-27 and the rows sort by year. A number given is recorded by the
// document that carries it, so the running number is left out of the change records and a year writes one when it is opened or
// reopened.
internal sealed class NumberSeriesYearConfiguration : IEntityTypeConfiguration<NumberSeriesYear>
{
    public void Configure(EntityTypeBuilder<NumberSeriesYear> builder)
    {
        builder.ToTable(NumberingTables.Years, NumberingTables.Schema, table => table.ExcludeFromMigrations());
        builder.RecordChangesAs(NumberingTables.YearsChangeKey);
        builder.FileChangesUnder(y => y.SeriesId);
        builder.HasKey(y => y.Id);
        builder.Property(y => y.Id).ValueGeneratedNever();
        builder.Property(y => y.Year)
            .HasConversion(year => year.Label, label => FinancialYear.Parse(label, CultureInfo.InvariantCulture))
            .HasMaxLength(NumberingTables.YearLength)
            .IsUnicode(false)
            .IsFixedLength();
        builder.ComplexProperty(y => y.Format, NumberFormatMapping.Configure);
        builder.Property(y => y.NextNumber).ExcludeFromChangeRecords();
        builder.Ignore(y => y.InUse);
        builder.Ignore(y => y.LastNumber);
        builder.HasOne<NumberSeries>().WithMany().HasForeignKey(y => y.SeriesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(y => new { y.SeriesId, y.Year }).IsUnique();
    }
}
