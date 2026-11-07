using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Persistence;

internal sealed class IndianStateConfiguration : IEntityTypeConfiguration<IndianState>
{
    public void Configure(EntityTypeBuilder<IndianState> builder)
    {
        builder.ToTable("IndianStates");
        builder.RecordChangesAs("platform.reference-data.indian-states");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.GstCode).HasMaxLength(IndianState.GstCodeLength).IsFixedLength().IsUnicode(false);
        builder.Property(s => s.Name).HasMaxLength(IndianState.NameMaxLength);
        builder.Property(s => s.Source).HasMaxLength(IndianState.SourceMaxLength);
        builder.HasIndex(s => new { s.GstCode, s.ValidFrom }).IsUnique();
    }
}
