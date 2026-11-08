using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Numbering.Sample;

// The unique number per kind and year is the table's own check that no number is given twice.
public sealed class SampleInvoiceConfiguration : IEntityTypeConfiguration<SampleInvoice>
{
    public void Configure(EntityTypeBuilder<SampleInvoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.DocumentType).HasMaxLength(SampleInvoice.DocumentTypeMaxLength).IsUnicode(false);
        builder.Property(i => i.Number).HasMaxLength(SampleInvoice.NumberMaxLength).IsUnicode(false);
        builder.Property(i => i.Year).HasMaxLength(7).IsUnicode(false).IsFixedLength();
        builder.HasIndex(i => new { i.DocumentType, i.Year, i.Number }).IsUnique();
    }
}
