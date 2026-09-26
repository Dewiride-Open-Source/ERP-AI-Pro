using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Persistence;

internal sealed class UploadReservationConfiguration : IEntityTypeConfiguration<UploadReservation>
{
    public void Configure(EntityTypeBuilder<UploadReservation> builder)
    {
        builder.ToTable("UploadReservations");
        builder.HasKey(r => r.ContentId);
        builder.HasIndex(r => r.ReservedAt);
    }
}
