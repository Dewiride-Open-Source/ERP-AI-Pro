using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Notes;

public sealed class SampleNoteConfiguration : IEntityTypeConfiguration<SampleNote>
{
    public const string ChangeKey = "test.sample.notes";

    public void Configure(EntityTypeBuilder<SampleNote> builder)
    {
        builder.ToTable("SampleNotes");
        builder.RecordChangesAs(ChangeKey);
        builder.FileChangesUnder(n => n.SampleId);
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Text).HasMaxLength(SampleNote.TextMaxLength);
        builder.PrimitiveCollection(n => n.Tags);
        builder.HasOne<SampleAggregate>().WithMany().HasForeignKey(n => n.SampleId).OnDelete(DeleteBehavior.Restrict);
    }
}
