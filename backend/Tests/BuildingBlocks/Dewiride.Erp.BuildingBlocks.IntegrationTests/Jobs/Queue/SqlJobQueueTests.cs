using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Queue;

public sealed class SqlJobQueueTests
{
    private static readonly Guid Person = new("0198f6a2-7c1e-7b3a-9d4e-2f6a8b1c3d5e");

    [Fact]
    public async Task EnqueueAsync_SameKeyAndPayload_ReturnsTheExistingRun()
    {
        var name = JobsDatabase.NewName("same-key");
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(JobScript.Succeeding()).AddJob<PayloadJob, ItemPayload>(JobsDatabase.Definition(name)));
        var key = JobsDatabase.NewKey();
        var payload = new ItemPayload(Guid.CreateVersion7(), 3);

        var first = await EnqueueAsync(jobs, payload, key);
        var second = await EnqueueAsync(jobs, payload with { }, key);
        var third = await EnqueueAsync(jobs, payload, key.ToUpperInvariant());

        Assert.Equal(first, second);
        Assert.Equal(first, third);
        Assert.Equal(1, await jobs.QueryAsync((context, cancellationToken) => context.Runs.CountAsync(r => r.JobName == name, cancellationToken)));
        Assert.Equal([JobRunEventKind.Enqueued], (await jobs.EventsAsync(first)).Select(e => e.Kind));
    }

    [Fact]
    public async Task EnqueueAsync_SameKeyOtherPayload_Throws()
    {
        var name = JobsDatabase.NewName("other-payload");
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(JobScript.Succeeding()).AddJob<PayloadJob, ItemPayload>(JobsDatabase.Definition(name)));
        var key = JobsDatabase.NewKey();
        var payload = new ItemPayload(Guid.CreateVersion7(), 3);
        await EnqueueAsync(jobs, payload, key);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => EnqueueAsync(jobs, payload with { Quantity = 4 }, key));

        Assert.Contains(name, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnqueueAsync_ReservedOccurrencePrefix_Throws()
    {
        var name = JobsDatabase.NewName("reserved");
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(JobScript.Succeeding()).AddJob<ScriptedJob>(JobsDatabase.Definition(name)));

        await Assert.ThrowsAsync<ArgumentException>(() => jobs.EnqueueAsync<ScriptedJob>("occurrence:2026-11-02T00:30+05:30"));

        Assert.Equal(0, await jobs.QueryAsync((context, cancellationToken) => context.Runs.CountAsync(r => r.JobName == name, cancellationToken)));
    }

    [Fact]
    public async Task EnqueueAsync_UnknownJob_Throws()
    {
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(JobScript.Succeeding()).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("known"))));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => EnqueueAsync(jobs, new ItemPayload(Guid.CreateVersion7(), 1), JobsDatabase.NewKey()));

        Assert.Contains(nameof(PayloadJob), refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnqueueAsync_Committed_RecordsTheActorAndWakesTheRunner()
    {
        var name = JobsDatabase.NewName("actor");
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(JobScript.Succeeding()).AddJob<ScriptedJob>(JobsDatabase.Definition(name)));
        JobsDatabase.ActorOf(jobs.First).ActorId = Person;
        var signal = jobs.First.Services.GetRequiredService<JobSignal>();

        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());

        var run = await jobs.RunAsync(id);
        Assert.Equal((JobRunStatus.Pending, Person, 0, 0), (run.Status, run.RequestedBy, run.AttemptCount, run.FailureCount));
        Assert.Equal(jobs.FirstClock.GetUtcNow(), run.DueAt);
        Assert.Null(run.Payload);
        var enqueued = Assert.Single(await jobs.EventsAsync(id));
        Assert.Equal((JobRunEventKind.Enqueued, Person), (enqueued.Kind, enqueued.ActorId));
        Assert.True(signal.WaitAsync(TimeSpan.FromMinutes(1), jobs.FirstClock, TestContext.Current.CancellationToken).IsCompleted);
    }

    private static async Task<JobRunId> EnqueueAsync(JobsDatabase jobs, ItemPayload payload, string key)
    {
        await using var scope = jobs.First.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueAsync<PayloadJob, ItemPayload>(payload, key, TestContext.Current.CancellationToken);
    }
}
