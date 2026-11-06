using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Running;

// The loop of the first runner's host, started and stopped by hand. The host's shutdown timeout is the token the test gives
// StopAsync and cancels when it chooses, so no test waits for real time to pass. The test of the loop's start gate builds the
// service over a lifetime it starts by hand, which tells it when the loop reached the gate.
public sealed class JobRunnerServiceTests
{
    [Fact]
    public async Task StopAsync_JobRunning_ClaimsNothingMoreAndCancelsTheJobOnlyWhenTheShutdownTimeoutFires()
    {
        var script = JobScript.Succeeding().WaitingForItsToken();
        await using var jobs = Create(script, "stop");
        var first = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var service = await StartAsync(jobs);
        await script.Started;

        using var shutdown = new CancellationTokenSource();
        var stopping = service.StopAsync(shutdown.Token);
        await service.ExecuteTask!;
        var second = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        jobs.Advance(PollInterval(jobs));

        Assert.False(stopping.IsCompleted);
        Assert.False(script.TokenFired);
        Assert.Equal(JobRunStatus.Running, (await jobs.RunAsync(first)).Status);
        await shutdown.CancelAsync();
        await stopping;

        Assert.True(script.TokenFired);
        Assert.Equal((JobRunStatus.Pending, 0), ((await jobs.RunAsync(second)).Status, (await jobs.RunAsync(second)).AttemptCount));
        Assert.Single(script.Executions);
    }

    [Fact]
    public async Task StopAsync_JobCancelledByShutdown_ReturnsTheRunToPendingWithoutAFailure()
    {
        var script = JobScript.Succeeding().WaitingForItsToken();
        await using var jobs = Create(script, "interrupt");
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var service = await StartAsync(jobs);
        await script.Started;

        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();
        await service.StopAsync(shutdown.Token);

        var run = await jobs.RunAsync(id);
        Assert.Equal((JobRunStatus.Pending, 0, 1, (Guid?)null), (run.Status, run.FailureCount, run.AttemptCount, run.LeaseToken));
        Assert.Equal(jobs.FirstClock.GetUtcNow(), run.DueAt);
        Assert.Equal(1, Assert.Single(await jobs.EventsAsync(id), e => e.Kind == JobRunEventKind.Interrupted).Attempt);
    }

    [Fact]
    public async Task ExecuteAsync_ApplicationStopping_ClaimsNothingMoreWhileTheRunningJobGoesOn()
    {
        var script = JobScript.Succeeding().WaitingForRelease();
        await using var jobs = Create(script, "stopping");
        var first = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var service = await StartAsync(jobs);
        await script.Started;

        JobsDatabase.LifetimeOf(jobs.First).StopApplication();
        await service.ExecuteTask!;
        var second = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        jobs.Advance(PollInterval(jobs));
        script.Release();
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(JobRunStatus.Succeeded, (await jobs.RunAsync(first)).Status);
        Assert.Equal((JobRunStatus.Pending, 0), ((await jobs.RunAsync(second)).Status, (await jobs.RunAsync(second)).AttemptCount));
    }

    [Fact]
    public async Task ExecuteAsync_BeforeApplicationStarted_ClaimsNothing()
    {
        var script = JobScript.Succeeding();
        await using var jobs = Create(script, "not-started");
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        using var lifetime = new ManualApplicationLifetime();
        using var service = ServiceOf(jobs, lifetime);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await lifetime.StartRequested.WaitAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        jobs.First.Services.GetRequiredService<JobSignal>().Wake();
        jobs.Advance(PollInterval(jobs) * 3);

        Assert.Equal((JobRunStatus.Pending, 0), ((await jobs.RunAsync(id)).Status, (await jobs.RunAsync(id)).AttemptCount));
        Assert.Empty(script.Executions);
        lifetime.NotifyStarted();
        await script.Started;
        await service.StopAsync(CancellationToken.None);
        Assert.Equal(JobRunStatus.Succeeded, (await jobs.RunAsync(id)).Status);
    }

    [Fact]
    public async Task ExecuteAsync_PollFails_LogsAndPollsAgain()
    {
        var firstFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var missing = new SqlConnectionStringBuilder(SqlTestDatabase.Current.ConnectionString) { InitialCatalog = $"ErpAiProJobsMissing_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}" };
        await using var jobs = JobsDatabase.Create(
            services => services
                .AddSingleton(JobScript.Succeeding())
                .AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("poll-fails")))
                .Configure<FakeLogCollectorOptions>(options => options.OutputSink = line =>
                {
                    if (line.Contains("Polling for due jobs failed again", StringComparison.Ordinal))
                    {
                        secondFailure.TrySetResult();
                    }
                    else if (line.Contains("Polling for due jobs failed;", StringComparison.Ordinal))
                    {
                        firstFailure.TrySetResult();
                    }
                }),
            settings: new Dictionary<string, string?> { ["Erp:Platform:Database:MaxRetryCount"] = "0" },
            connectionString: missing.ConnectionString);
        var service = await StartAsync(jobs);

        await firstFailure.Task;
        jobs.First.Services.GetRequiredService<JobSignal>().Wake();
        await secondFailure.Task;
        await service.StopAsync(CancellationToken.None);

        var records = JobsDatabase.LogsOf(jobs.First).GetSnapshot().Where(record => record.Message.StartsWith("Polling for due jobs failed", StringComparison.Ordinal)).ToList();
        Assert.Equal(LogLevel.Warning, records[0].Level);
        Assert.NotNull(records[0].Exception);
        Assert.All(records.Skip(1), record => Assert.Equal(LogLevel.Debug, record.Level));
    }

    [Fact]
    public async Task ExecuteAsync_JobClaimedAndRun_LogsTheStoresExecutedCommandsAtDebug()
    {
        var script = JobScript.Succeeding();
        await using var jobs = Create(script, "quiet-store");
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var service = await StartAsync(jobs);

        await script.Started;
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(JobRunStatus.Succeeded, (await jobs.RunAsync(id)).Status);
        var commands = JobsDatabase.LogsOf(jobs.First).GetSnapshot()
            .Where(record => record.Category == DbLoggerCategory.Database.Command.Name && record.Id == RelationalEventId.CommandExecuted)
            .ToList();
        Assert.NotEmpty(commands);
        Assert.All(commands, record => Assert.Equal(LogLevel.Debug, record.Level));
    }

    [Fact]
    public async Task ExecuteAsync_RunSourceEnqueuesARun_ClaimsAndRunsIt()
    {
        var script = JobScript.Succeeding();
        await using var jobs = JobsDatabase.Create(services => services
            .AddSingleton(script)
            .AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("sourced")))
            .AddJobRunSource<ScriptedRunSource>());
        var key = JobsDatabase.NewKey();
        SourceOf(jobs).Key = key;
        var service = await StartAsync(jobs);

        await script.Started;
        await service.StopAsync(CancellationToken.None);

        var run = await jobs.QueryAsync((context, cancellationToken) => context.Runs.AsNoTracking().SingleAsync(r => r.IdempotencyKey == key, cancellationToken));
        Assert.Equal(JobRunStatus.Succeeded, run.Status);
        Assert.Single(script.Executions);
    }

    [Fact]
    public async Task ExecuteAsync_RunSourceFails_LogsItsStreakWhileTheRunnerStillClaims()
    {
        var script = JobScript.Succeeding();
        var failedAgain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var jobs = JobsDatabase.Create(services => services
            .AddSingleton(script)
            .AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("source-fails")))
            .AddJobRunSource<ScriptedRunSource>()
            .Configure<FakeLogCollectorOptions>(options => options.OutputSink = line =>
            {
                if (line.Contains($"{nameof(ScriptedRunSource)} failed again", StringComparison.Ordinal))
                {
                    failedAgain.TrySetResult();
                }
                else if (line.Contains($"{nameof(ScriptedRunSource)} works again", StringComparison.Ordinal))
                {
                    recovered.TrySetResult();
                }
            }));
        SourceOf(jobs).FailPolls(2);
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var signal = jobs.First.Services.GetRequiredService<IJobRunnerSignal>();
        var service = await StartAsync(jobs);

        await script.Started;
        signal.Wake();
        await failedAgain.Task;
        signal.Wake();
        await recovered.Task;
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(JobRunStatus.Succeeded, (await jobs.RunAsync(id)).Status);
        var logs = JobsDatabase.LogsOf(jobs.First).GetSnapshot();
        var streak = logs.Where(record => record.Message.StartsWith($"The run source {nameof(ScriptedRunSource)}", StringComparison.Ordinal)).ToList();
        Assert.Equal(new[] { LogLevel.Warning, LogLevel.Debug, LogLevel.Information }, streak.Select(record => record.Level));
        Assert.All(streak.Take(2), record => Assert.IsType<InvalidOperationException>(record.Exception));
        Assert.Contains("after 2 failed polls", streak[2].Message, StringComparison.Ordinal);
        Assert.DoesNotContain(logs, record => record.Message.StartsWith("Polling for due jobs failed", StringComparison.Ordinal) && record.Exception is InvalidOperationException { Message: "The scripted run source failed." });
    }

    private static ScriptedRunSource SourceOf(JobsDatabase jobs) => jobs.First.Services.GetServices<IJobRunSource>().OfType<ScriptedRunSource>().Single();

    private static JobsDatabase Create(JobScript script, string scenario) =>
        JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName(scenario))));

    private static JobRunnerService ServiceOf(JobsDatabase jobs) => jobs.First.Services.GetServices<IHostedService>().OfType<JobRunnerService>().Single();

    private static JobRunnerService ServiceOf(JobsDatabase jobs, IHostApplicationLifetime lifetime)
    {
        var services = jobs.First.Services;

        return new JobRunnerService(
            services.GetRequiredService<JobRunner>(),
            services.GetRequiredService<JobScheduler>(),
            services.GetServices<IJobRunSource>(),
            services.GetRequiredService<JobSignal>(),
            lifetime,
            services.GetRequiredService<IOptions<JobRunnerOptions>>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILogger<JobRunnerService>>());
    }

    private static async Task<JobRunnerService> StartAsync(JobsDatabase jobs)
    {
        var service = ServiceOf(jobs);
        await service.StartAsync(TestContext.Current.CancellationToken);
        JobsDatabase.LifetimeOf(jobs.First).NotifyStarted();

        return service;
    }

    private static TimeSpan PollInterval(JobsDatabase jobs) => jobs.First.Services.GetRequiredService<IOptions<JobRunnerOptions>>().Value.PollInterval;
}
