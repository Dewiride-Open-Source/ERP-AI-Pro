using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Persistence;

internal sealed class DownloadRedemptionConfiguration : IEntityTypeConfiguration<DownloadRedemption>
{
    public void Configure(EntityTypeBuilder<DownloadRedemption> builder)
    {
        builder.ToTable("DownloadRedemptions");
        builder.HasKey(r => r.Id);
        builder.HasOne<DownloadLink>().WithMany().HasForeignKey(r => r.LinkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Attachment>().WithMany().HasForeignKey(r => r.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.AttachmentId, r.RedeemedAt });
    }
}
