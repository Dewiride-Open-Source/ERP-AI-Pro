using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence;

// The columns are a value compared by what they declare, so a reseed of the same file writes nothing; a change to them is one
// change of the set, recorded as its text before and after.
internal sealed class ParameterSetConfiguration : IEntityTypeConfiguration<ParameterSet>
{
    public void Configure(EntityTypeBuilder<ParameterSet> builder)
    {
        builder.ToTable("ParameterSets");
        builder.RecordChangesAs("compliance.statutory-parameters.parameter-sets");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Key).HasMaxLength(ParameterSet.KeyMaxLength).IsUnicode(false);
        builder.Property(s => s.Title).HasMaxLength(ParameterSet.TitleMaxLength);
        builder.Property(s => s.Description).HasMaxLength(ParameterSet.DescriptionMaxLength);
        builder.Property(s => s.Basis).HasConversion<byte>();
        builder.Property(s => s.AsOf).HasConversion<byte>();
        builder.Property(s => s.Columns)
            .HasConversion(
                columns => ParameterColumnSetJson.Write(columns),
                text => ParameterColumnSetJson.Read(text),
                new ValueComparer<ParameterColumnSet>((left, right) => Equals(left, right), columns => columns.GetHashCode(), columns => columns))
            .HasColumnType("json");
        builder.HasIndex(s => s.Key).IsUnique();
    }
}
