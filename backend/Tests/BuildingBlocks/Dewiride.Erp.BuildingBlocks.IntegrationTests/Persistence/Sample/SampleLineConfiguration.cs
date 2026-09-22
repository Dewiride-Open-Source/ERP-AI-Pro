using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed class SampleLineConfiguration : IEntityTypeConfiguration<SampleLine>
{
    public void Configure(EntityTypeBuilder<SampleLine> builder)
    {
        builder.ToTable("SampleLines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Description).HasMaxLength(SampleLine.DescriptionMaxLength);
    }
}
