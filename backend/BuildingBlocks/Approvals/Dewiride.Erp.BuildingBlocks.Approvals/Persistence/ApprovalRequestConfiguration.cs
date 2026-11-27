using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Persistence;

// Every context of a module that asks for approval maps this table, so a request commits with the record it approves; the table
// belongs to ApprovalsDbContext, so the mapping leaves it out of every other context's migrations. One request of a kind waits for a
// record at a time: a second, however close behind, is refused by the unique index of the waiting requests, whose filter names
// ApprovalState.Submitted by its stored number.
internal sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable(ApprovalTables.Requests, ApprovalTables.Schema, table => table.ExcludeFromMigrations());
        builder.RecordChangesAs("platform.approvals.requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Kind).HasMaxLength(ApprovalRequest.KindMaxLength).IsUnicode(false);
        builder.Property(r => r.SubjectType).HasMaxLength(ApprovalRequest.SubjectTypeMaxLength).IsUnicode(false);
        builder.Property(r => r.State).HasConversion<byte>();
        builder.Ignore(r => r.WaitingStep);
        builder.HasMany(r => r.Steps).WithOne().HasForeignKey(s => s.RequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.Kind, r.SubjectId }).IsUnique().HasFilter("[State] = 1");
        builder.HasIndex(r => new { r.Kind, r.SubjectId, r.SubmittedAt });
        builder.HasIndex(r => new { r.RequestedFor, r.SubmittedAt });
        builder.HasIndex(r => new { r.CreatedBy, r.SubmittedAt });
        builder.HasIndex(r => new { r.State, r.SubmittedAt });
    }
}
