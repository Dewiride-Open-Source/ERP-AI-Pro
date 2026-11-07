using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Persistence;

internal sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries");
        builder.RecordChangesAs("platform.reference-data.countries");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(Country.CodeLength).IsFixedLength().IsUnicode(false);
        builder.Property(c => c.Name).HasMaxLength(Country.NameMaxLength);
        builder.HasIndex(c => c.Code).IsUnique();
    }
}
