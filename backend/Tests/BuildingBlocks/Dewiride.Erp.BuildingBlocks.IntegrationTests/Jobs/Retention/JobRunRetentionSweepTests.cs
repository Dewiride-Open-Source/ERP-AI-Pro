using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Jobs;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Retention;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.Testing.Jobs;
using Dewiride.Erp.Testing.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Retention;

// A sweep deletes every finished run past its retention in the per-process test database, not only its own test's. These runs
// finish in 2001 and every sweep's clock is set before 2003, while the other test classes write their runs in 2026, or in 2000
// without finishing them, so no sweep here reaches another class's run; schedule rows are swept for every job the host does not
// schedule, so the class runs alone.
[Collection(JobScheduleHolders.Name)]
public sealed class JobRunRetentionSweepTests
{
    private static readonly DateTimeOffset LongAgo = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Recently = new(2001, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset SweptAt = new(2002, 3, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SweepAsync_FinishedRunsPastTheRetention_AreDeletedWithTheirEventsAndTheRestKept()
    {
        var name = JobsDatabase.NewName("retention");
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(JobScript.Succeeding()).AddJob<ScriptedJob>(JobsDatabase.Definition(name)), LongAgo);
        var old = new Dictionary<JobRunStatus, JobRunId>();
        foreach (var status in Enum.GetValues<JobRunStatus>())
        {
            old[status] = await SeedAsync(jobs, name, status);
        }

        jobs.FirstClock.SetUtcNow(Recently);
        var recent = await SeedAsync(jobs, name, JobRunStatus.Succeeded);
        jobs.FirstClock.SetUtcNow(SweptAt);

        await SweepAsync(jobs);

        var kept = await jobs.QueryAsync((context, cancellationToken) => context.Runs.AsNoTracking().Where(r => r.JobName == name).Select(r => r.Id).ToListAsync(cancellationToken));
        Assert.Equal(
            new[] { old[JobRunStatus.Pending], old[JobRunStatus.Running], old[JobRunStatus.Failed], recent }.Select(id => id.Value).Order(),
            kept.Select(id => id.Value).Order());
        var deleted = new[] { old[JobRunStatus.Succeeded], old[JobRunStatus.DeadLettered], old[JobRunStatus.Cancelled] };
        Assert.Equal(0, await jobs.QueryAsync((context, cancellationToken) => context.Events.CountAsync(e => deleted.Contains(e.RunId), cancellationToken)));
        Assert.NotEmpty(await jobs.EventsAsync(recent));
    }

    [Fact]
    public async Task SweepAsync_ScheduleOfAJobNoLongerScheduled_IsDeletedAndTheOthersKept()
    {
        var scheduled = JobsDatabase.NewName("scheduled");
        var retired = JobsDatabase.NewName("retired");
        await using var jobs = JobsDatabase.Create(services => services
            .AddSingleton(JobScript.Succeeding())
            .AddSingleton<IOptionsMonitor<TestScheduleOptions>>(new TestScheduleOptions())
            .AddRecurringJob<ScriptedJob, TestScheduleOptions>(JobsDatabase.Definition(scheduled), options => options.Expression), LongAgo);
        await jobs.QueryAsync(async (context, cancellationToken) =>
        {
            context.Schedules.AddRange(JobSchedule.Start(scheduled, "30 0 * * *", SweptAt), JobSchedule.Start(retired, "0 1 * * *", SweptAt));
            return await context.SaveChangesAsync(cancellationToken);
        });

        await SweepAsync(jobs);

        var names = await jobs.QueryAsync((context, cancellationToken) => context.Schedules.AsNoTracking().Where(s => s.JobName == scheduled || s.JobName == retired).Select(s => s.JobName).ToListAsync(cancellationToken));
        Assert.Equal([scheduled], names);
    }

    [Fact]
    public async Task SweepAsync_MoreRunsThanABatch_DeletesThemAllInStatementsOfOneOrderedBatchEach()
    {
        var name = JobsDatabase.NewName("batches");
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(JobScript.Succeeding()).AddJob<ScriptedJob>(JobsDatabase.Definition(name)), LongAgo);
        for (var index = 0; index <= JobRunRetentionSweep.BatchSize; index++)
        {
            await SeedAsync(jobs, name, JobRunStatus.Succeeded);
        }

        var cutoff = SweptAt - new JobsOptions().FinishedRunRetention;
        var due = await jobs.QueryAsync((context, cancellationToken) => context.Runs.CountAsync(r => r.FinishedAt < cutoff, cancellationToken));
        var commands = new CommandLog();
        var clock = new FakeTimeProvider(SweptAt);
        await using (var context = CreateContext(clock, commands))
        {
            var sweep = new JobRunRetentionSweep(context, jobs.First.Services.GetRequiredService<JobCatalog>(), clock, Options.Create(new JobsOptions()), new FakeLogger<JobRunRetentionSweep>());
            await sweep.SweepAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, await jobs.QueryAsync((context, cancellationToken) => context.Runs.CountAsync(r => r.JobName == name, cancellationToken)));
        var deletions = commands.Commands.Where(command => command.Contains("[platform_jobs].[JobRuns]", StringComparison.Ordinal) && command.StartsWith("DELETE", StringComparison.Ordinal)).ToList();
        Assert.Equal((due / JobRunRetentionSweep.BatchSize) + 1, deletions.Count);
        Assert.All(deletions, command =>
        {
            Assert.Contains("SELECT TOP(@p) [j0].[Id]", command, StringComparison.Ordinal);
            Assert.Contains("ORDER BY [j0].[FinishedAt], [j0].[Id]", command, StringComparison.Ordinal);
        });
    }

    private static Task<JobRunId> SeedAsync(JobsDatabase jobs, string name, JobRunStatus status) =>
        JobRunSeeder.SeedAsync(jobs.First.Services, new JobRunSeed(name, JobsDatabase.NewKey(), status), TestContext.Current.CancellationToken);

    private static async Task SweepAsync(JobsDatabase jobs)
    {
        jobs.FirstClock.SetUtcNow(SweptAt);
        await using var scope = jobs.First.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<JobRunRetentionSweep>().SweepAsync(TestContext.Current.CancellationToken);
    }

    private static JobsDbContext CreateContext(FakeTimeProvider clock, CommandLog commands)
    {
        var options = new DbContextOptionsBuilder<JobsDbContext>();
        ModuleDbContextRegistration.Configure(options, new DatabaseOptions { ConnectionString = SqlTestDatabase.Current.ConnectionString }, JobsDbContext.SchemaName)
            .AddInterceptors(new AuditingSaveChangesInterceptor(new TestActorContext(), new TestRequestOrigin(), clock), new BulkWriteGuardInterceptor(), commands)
            .ConfigureWarnings(warnings => warnings.Log(CoreEventId.ManyServiceProvidersCreatedWarning).Throw(CoreEventId.RowLimitingOperationWithoutOrderByWarning));

        return new JobsDbContext(options.Options);
    }
}
