using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Persistence;

// The database deletes a run's events with it, so the retention sweep removes a finished run in one statement and an event
// never outlives its run; the change tracker never deletes either.
internal sealed class JobRunEventConfiguration : IEntityTypeConfiguration<JobRunEvent>
{
    public void Configure(EntityTypeBuilder<JobRunEvent> builder)
    {
        builder.ToTable("JobRunEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasConversion<byte>();
        builder.Property(e => e.Runner).HasMaxLength(JobRun.LeaseHolderMaxLength).IsUnicode(false);
        builder.Property(e => e.TraceId).HasMaxLength(JobRunEvent.TraceIdLength).IsUnicode(false);
        builder.Property(e => e.ErrorCode).HasMaxLength(JobRun.ErrorCodeMaxLength).IsUnicode(false);
        builder.Property(e => e.ErrorMessage).HasMaxLength(JobRun.ErrorMessageMaxLength);
        builder.HasOne<JobRun>().WithMany().HasForeignKey(e => e.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.RunId, e.OccurredAt });
    }
}
