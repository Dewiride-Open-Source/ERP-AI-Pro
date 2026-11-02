using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Testing.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Running;

public sealed class JobRetryTests
{
    private static readonly JobRetryPolicy FourAttempts = new(4, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));

    [Fact]
    public async Task RunAsync_UnavailableUntilTheLastAttempt_RetriesWithDoublingDelaysThenDeadLetters()
    {
        var script = JobScript.Failing(Error.Unavailable("test.unavailable", "The dependency did not answer."));
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("retry"), FourAttempts)));
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var driver = new JobRunnerDriver(jobs.First.Services);
        var retries = new List<TimeSpan>();

        for (var attempt = 1; attempt < FourAttempts.MaxAttempts; attempt++)
        {
            var outcome = await driver.RunAsync(id, TestContext.Current.CancellationToken);
            var run = await jobs.RunAsync(id);
            Assert.Equal((JobRunStatus.Failed, "test.unavailable", attempt, attempt), (outcome.Status, outcome.ErrorCode, run.FailureCount, run.AttemptCount));
            retries.Add(run.DueAt - jobs.FirstClock.GetUtcNow());
            jobs.Advance(run.DueAt - jobs.FirstClock.GetUtcNow() - TimeSpan.FromSeconds(1));
            Assert.Null(await jobs.FirstRunner.TryRunAsync(id, TestContext.Current.CancellationToken));
            Assert.Equal(0, await jobs.FirstRunner.StartDueAsync(TestContext.Current.CancellationToken));
            var waiting = await jobs.RunAsync(id);
            Assert.Equal((JobRunStatus.Failed, attempt), (waiting.Status, waiting.AttemptCount));
            jobs.Advance(TimeSpan.FromSeconds(1));
        }

        var last = await driver.RunAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4)], retries);
        Assert.Equal(JobRunStatus.DeadLettered, last.Status);
        var final = await jobs.RunAsync(id);
        Assert.Equal((4, 4, jobs.FirstClock.GetUtcNow()), (final.FailureCount, final.AttemptCount, final.FinishedAt!.Value));
        var events = await jobs.EventsAsync(id);
        Assert.Equal(4, events.Count(e => e.Kind == JobRunEventKind.Started));
        Assert.Equal([1, 2, 3], events.Where(e => e.Kind == JobRunEventKind.Failed).Select(e => e.Attempt!.Value).Order());
        Assert.Equal(4, Assert.Single(events, e => e.Kind == JobRunEventKind.DeadLettered).Attempt);
        Assert.Equal(4, script.Executions.Count);
    }

    [Fact]
    public async Task RunAsync_PermanentError_DeadLettersAtOnce()
    {
        var script = JobScript.Failing(Error.Forbidden("directory.access-denied", "The ERP may not read the directory."));
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("permanent"), FourAttempts)));
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());

        var outcome = await new JobRunnerDriver(jobs.First.Services).RunAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal((JobRunStatus.DeadLettered, "directory.access-denied"), (outcome.Status, outcome.ErrorCode));
        var run = await jobs.RunAsync(id);
        Assert.Equal((1, "directory.access-denied", "The ERP may not read the directory."), (run.FailureCount, run.LastErrorCode, run.LastErrorMessage));
        var gaveUp = Assert.Single(await jobs.EventsAsync(id), e => e.Kind == JobRunEventKind.DeadLettered);
        Assert.Equal((outcome.TraceId, "directory.access-denied"), (gaveUp.TraceId, gaveUp.ErrorCode));
    }

    [Fact]
    public async Task RunAsync_JobThrows_StoresOnlyTheExceptionTypeAndLogsTheException()
    {
        var script = new JobScript { Run = (_, _) => throw new InvalidOperationException("Login failed for user 'erp_local' with password 'hunter2'.") };
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("throws"))));
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());

        var outcome = await new JobRunnerDriver(jobs.First.Services).RunAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal((JobRunStatus.Failed, "job.exception"), (outcome.Status, outcome.ErrorCode));
        var run = await jobs.RunAsync(id);
        Assert.Contains(nameof(InvalidOperationException), run.LastErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", run.LastErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(await jobs.EventsAsync(id), e => e.ErrorMessage?.Contains("hunter2", StringComparison.Ordinal) == true);
        var threw = Assert.Single(JobsDatabase.LogsOf(jobs.First).GetSnapshot(), record => record.Level == LogLevel.Warning && record.Exception is InvalidOperationException);
        Assert.Equal(nameof(InvalidOperationException), threw.GetStructuredStateValue("ExceptionType"));
    }

    [Fact]
    public async Task RunAsync_AttemptOverrunsItsTimeout_CancelsItsTokenAndRetries()
    {
        var script = JobScript.Succeeding().WaitingForItsToken();
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("timeout"), attemptTimeout: TimeSpan.FromMinutes(1))));
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var attempt = new JobRunnerDriver(jobs.First.Services).RunAsync(id, TestContext.Current.CancellationToken);
        await script.Started;

        jobs.FirstClock.Advance(TimeSpan.FromSeconds(59));
        Assert.False(script.TokenFired);
        jobs.FirstClock.Advance(TimeSpan.FromSeconds(1));
        var outcome = await attempt;

        Assert.True(script.TokenFired);
        Assert.Equal((JobRunStatus.Failed, "job.timed-out"), (outcome.Status, outcome.ErrorCode));
        Assert.Contains(JobsDatabase.LogsOf(jobs.First).GetSnapshot(), record => record.Level == LogLevel.Warning && record.Message.Contains("ran past its limit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_ScopeThatAnswersAPerson_DoesNotRunTheJob()
    {
        var script = JobScript.Succeeding();
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("person"))));
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        JobsDatabase.ActorOf(jobs.First).ActorId = Guid.CreateVersion7();

        var outcome = await new JobRunnerDriver(jobs.First.Services).RunAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal((JobRunStatus.DeadLettered, "job.actor-not-system"), (outcome.Status, outcome.ErrorCode));
        Assert.Empty(script.Executions);
        Assert.Contains(JobsDatabase.LogsOf(jobs.First).GetSnapshot(), record => record.Level == LogLevel.Error && record.Message.Contains("someone other than the system", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_PayloadNoLongerReadable_DeadLettersWithoutRunningTheJob()
    {
        var script = JobScript.Succeeding();
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<PayloadJob, ItemPayload>(JobsDatabase.Definition(JobsDatabase.NewName("payload"))));
        JobRunId id;
        await using (var scope = jobs.First.Services.CreateAsyncScope())
        {
            id = await scope.ServiceProvider.GetRequiredService<IJobQueue>()
                .EnqueueAsync<PayloadJob, ItemPayload>(new ItemPayload(Guid.CreateVersion7(), 1), JobsDatabase.NewKey(), TestContext.Current.CancellationToken);
        }

        await jobs.QueryAsync((context, cancellationToken) => context.Runs.Where(r => r.Id == id).ExecuteUpdateAsync(setters => setters.SetProperty(r => r.Payload, """{"itemId":"not an id","quantity":1}"""), cancellationToken));
        var outcome = await new JobRunnerDriver(jobs.First.Services).RunAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal((JobRunStatus.DeadLettered, "job.payload-unreadable"), (outcome.Status, outcome.ErrorCode));
        Assert.Empty(script.Executions);
    }

    [Fact]
    public async Task RunAsync_Attempt_RunsAsTheSystemWithItsTraceAsTheCorrelation()
    {
        var script = JobScript.Succeeding();
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("system"))));
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());

        var outcome = await new JobRunnerDriver(jobs.First.Services).RunAsync(id, TestContext.Current.CancellationToken);

        var execution = Assert.Single(script.Executions);
        Assert.Equal((ActorIds.System, outcome.TraceId, 1), (execution.ActorId, execution.CorrelationId, execution.Attempt));
        Assert.Equal(JobRunStatus.Succeeded, outcome.Status);
        Assert.All((await jobs.EventsAsync(id)).Where(e => e.Kind != JobRunEventKind.Enqueued), e => Assert.Equal(outcome.TraceId, e.TraceId));
    }
}
