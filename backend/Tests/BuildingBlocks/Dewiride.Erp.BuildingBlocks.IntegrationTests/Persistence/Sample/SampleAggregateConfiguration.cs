using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed class SampleAggregateConfiguration : IEntityTypeConfiguration<SampleAggregate>
{
    public void Configure(EntityTypeBuilder<SampleAggregate> builder)
    {
        builder.ToTable("Samples");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(SampleAggregate.NameMaxLength);
        builder.HasMany(s => s.Lines).WithOne().HasForeignKey(l => l.SampleId);
        builder.ComplexProperty(s => s.Address, address =>
        {
            address.Property(a => a.Line1).HasMaxLength(SampleAggregate.Line1MaxLength);
            address.Property(a => a.City).HasMaxLength(SampleAggregate.CityMaxLength);
        });
    }
}
