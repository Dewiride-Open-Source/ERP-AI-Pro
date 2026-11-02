using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Persistence;

internal sealed class JobScheduleConfiguration : IEntityTypeConfiguration<JobSchedule>
{
    public void Configure(EntityTypeBuilder<JobSchedule> builder)
    {
        builder.ToTable("JobSchedules");
        builder.HasKey(s => s.JobName);
        builder.Property(s => s.JobName).HasMaxLength(JobNames.MaxLength).IsUnicode(false);
        builder.Property(s => s.Expression).HasMaxLength(JobSchedule.ExpressionMaxLength).IsUnicode(false);
    }
}
