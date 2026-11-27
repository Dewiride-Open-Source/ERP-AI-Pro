using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample;

public sealed class SampleClaimConfiguration : IEntityTypeConfiguration<SampleClaim>
{
    public const string ChangeKey = "test.approvals.claims";

    public void Configure(EntityTypeBuilder<SampleClaim> builder)
    {
        builder.ToTable("Claims");
        builder.RecordChangesAs(ChangeKey);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Title).HasMaxLength(SampleClaim.TitleMaxLength);
        builder.Property(c => c.State).HasConversion<byte>();
    }
}
