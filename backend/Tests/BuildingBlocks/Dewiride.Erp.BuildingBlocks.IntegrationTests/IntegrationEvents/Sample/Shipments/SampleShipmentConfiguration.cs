using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;

public sealed class SampleShipmentConfiguration : IEntityTypeConfiguration<SampleShipment>
{
    public const string ChangeKey = "test.events.shipments";

    public void Configure(EntityTypeBuilder<SampleShipment> builder)
    {
        builder.ToTable("Shipments");
        builder.RecordChangesAs(ChangeKey);
        builder.HasKey(s => s.Id);
    }
}
