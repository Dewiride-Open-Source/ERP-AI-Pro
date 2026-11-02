using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Persistence;

internal sealed class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
{
    public void Configure(EntityTypeBuilder<JobRun> builder)
    {
        builder.ToTable("JobRuns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.JobName).HasMaxLength(JobNames.MaxLength).IsUnicode(false);
        builder.Property(r => r.IdempotencyKey).HasMaxLength(JobKeys.MaxLength).IsUnicode(false);
        builder.Property(r => r.Payload).HasColumnType("json");
        builder.Property(r => r.Status).HasConversion<byte>();
        builder.Property(r => r.LeaseHolder).HasMaxLength(JobRun.LeaseHolderMaxLength).IsUnicode(false);
        builder.Property(r => r.LastErrorCode).HasMaxLength(JobRun.ErrorCodeMaxLength).IsUnicode(false);
        builder.Property(r => r.LastErrorMessage).HasMaxLength(JobRun.ErrorMessageMaxLength);
        builder.HasIndex(r => new { r.JobName, r.IdempotencyKey }).IsUnique();
        builder.HasIndex(r => new { r.Status, r.DueAt }).IncludeProperties(r => r.JobName);
        builder.HasIndex(r => r.LeaseExpiresAt).HasFilter("[LeaseExpiresAt] IS NOT NULL");
        builder.HasIndex(r => r.CreatedAt);
        builder.HasIndex(r => r.FinishedAt).HasFilter("[FinishedAt] IS NOT NULL");
    }
}
