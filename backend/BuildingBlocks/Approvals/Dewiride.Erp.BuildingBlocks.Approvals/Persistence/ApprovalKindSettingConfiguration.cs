using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Persistence;

internal sealed class ApprovalKindSettingConfiguration : IEntityTypeConfiguration<ApprovalKindSetting>
{
    public void Configure(EntityTypeBuilder<ApprovalKindSetting> builder)
    {
        builder.ToTable(ApprovalTables.KindSettings);
        builder.RecordChangesAs("platform.approvals.kind-settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Kind).HasMaxLength(ApprovalKind.KeyMaxLength).IsUnicode(false);
        builder.HasIndex(s => s.Kind).IsUnique();
    }
}
