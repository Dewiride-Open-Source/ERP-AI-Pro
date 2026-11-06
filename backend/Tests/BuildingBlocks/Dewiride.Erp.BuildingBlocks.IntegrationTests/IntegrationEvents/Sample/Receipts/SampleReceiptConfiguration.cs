using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Receipts;

public sealed class SampleReceiptConfiguration : IEntityTypeConfiguration<SampleReceipt>
{
    public const string ChangeKey = "test.events.receipts";

    public void Configure(EntityTypeBuilder<SampleReceipt> builder)
    {
        builder.ToTable("Receipts");
        builder.RecordChangesAs(ChangeKey);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RecordedBy).HasMaxLength(SampleReceipt.RecordedByMaxLength).IsUnicode(false);
        builder.HasIndex(r => r.ShipmentId);
    }
}
