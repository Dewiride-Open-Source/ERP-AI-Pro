using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence;

internal sealed class ParameterVersionConfiguration : IEntityTypeConfiguration<ParameterVersion>
{
    public void Configure(EntityTypeBuilder<ParameterVersion> builder)
    {
        builder.ToTable("ParameterVersions");
        builder.RecordChangesAs("compliance.statutory-parameters.parameter-versions");
        builder.FileChangesUnder(v => v.SetId);
        builder.HasKey(v => v.Id);
        builder.HasOne<ParameterSet>().WithMany().HasForeignKey(v => v.SetId).OnDelete(DeleteBehavior.Restrict);
        builder.PrimitiveCollection(v => v.Instruments).ElementType().HasMaxLength(ParameterVersion.InstrumentMaxLength);
        builder.HasIndex(v => new { v.SetId, v.From }).IsUnique();
    }
}
