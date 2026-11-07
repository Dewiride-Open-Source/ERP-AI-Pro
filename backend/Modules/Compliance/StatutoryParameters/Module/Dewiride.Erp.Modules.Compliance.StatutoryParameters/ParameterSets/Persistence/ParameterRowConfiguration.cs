using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence;

// A version's rows are those whose dates include its own, which the index on the set and the dates answers; the cells are a
// value compared by what they hold, so a reseed of the same row writes nothing.
internal sealed class ParameterRowConfiguration : IEntityTypeConfiguration<ParameterRow>
{
    public void Configure(EntityTypeBuilder<ParameterRow> builder)
    {
        builder.ToTable("ParameterRows");
        builder.RecordChangesAs("compliance.statutory-parameters.parameter-rows");
        builder.FileChangesUnder(r => r.SetId);
        builder.HasKey(r => r.Id);
        builder.HasOne<ParameterSet>().WithMany().HasForeignKey(r => r.SetId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(r => r.RowKey).HasMaxLength(ParameterColumn.CodeMaxLength);
        builder.Property(r => r.Source).HasMaxLength(ParameterRow.SourceMaxLength);
        builder.Property(r => r.Cells)
            .HasConversion(
                cells => ParameterCellsJson.Write(cells),
                text => ParameterCellsJson.Read(text),
                new ValueComparer<ParameterCells>((left, right) => Equals(left, right), cells => cells.GetHashCode(), cells => cells))
            .HasColumnType("json");
        builder.HasIndex(r => new { r.SetId, r.RowKey, r.From }).IsUnique();
        builder.HasIndex(r => new { r.SetId, r.From, r.Until });
    }
}
