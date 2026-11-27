using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Persistence;

// A step's changes are filed under its request, so the request's history shows its steps. The role is stored by name, as every
// built-in role is, and the two filtered indexes, which name ApprovalStepState.Waiting by its stored number, serve the lists of what
// waits for a person and for the holders of a role.
internal sealed class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        builder.ToTable(ApprovalTables.Steps, ApprovalTables.Schema, table => table.ExcludeFromMigrations());
        builder.RecordChangesAs("platform.approvals.steps");
        builder.FileChangesUnder(s => s.RequestId);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Title).HasMaxLength(ApprovalStep.TitleMaxLength);
        builder.Property(s => s.Rule).HasConversion<byte>();
        builder.Property(s => s.ApproverKind).HasConversion<byte>();
        builder.Property(s => s.ApproverRole).HasConversion<string>().HasMaxLength(ApprovalTables.RoleLength).IsUnicode(false);
        builder.Property(s => s.State).HasConversion<byte>();
        builder.Property(s => s.Comment).HasMaxLength(ApprovalStep.CommentMaxLength);
        builder.HasIndex(s => new { s.RequestId, s.Order }).IsUnique();
        builder.HasIndex(s => new { s.ApproverId, s.State }).HasFilter("[State] = 2");
        builder.HasIndex(s => new { s.ApproverRole, s.State }).HasFilter("[State] = 2");
        builder.HasIndex(s => new { s.DecidedBy, s.DecidedAt });
    }
}
