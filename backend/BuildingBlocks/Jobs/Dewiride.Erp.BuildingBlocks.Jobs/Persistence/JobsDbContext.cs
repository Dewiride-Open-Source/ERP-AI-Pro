using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Persistence;

internal sealed class JobsDbContext(DbContextOptions<JobsDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "platform_jobs";

    public DbSet<JobRun> Runs => Set<JobRun>();

    public DbSet<JobRunEvent> Events => Set<JobRunEvent>();

    public DbSet<JobSchedule> Schedules => Set<JobSchedule>();
}
