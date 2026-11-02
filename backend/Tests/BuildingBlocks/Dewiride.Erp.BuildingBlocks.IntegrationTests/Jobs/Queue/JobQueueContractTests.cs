using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.Testing.Jobs;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Queue;

// The in-process queue stands in for the database queue in module tests, so it must accept and refuse exactly what the database
// queue does.
public sealed class JobQueueContractTests
{
    [Theory]
    [InlineData("database")]
    [InlineData("in-process")]
    public async Task EnqueueAsync_EitherQueue_BehavesAlike(string queue)
    {
        await using var jobs = JobsDatabase.Create(services => services
            .AddSingleton(JobScript.Succeeding())
            .AddJob<PayloadJob, ItemPayload>(JobsDatabase.Definition(JobsDatabase.NewName("contract"))));
        await using var scope = jobs.First.Services.CreateAsyncScope();
        IJobQueue sut = queue == "database" ? scope.ServiceProvider.GetRequiredService<IJobQueue>() : new InProcessJobQueue(jobs.First.Services);
        var key = JobsDatabase.NewKey();
        var payload = new ItemPayload(Guid.CreateVersion7(), 2);
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await sut.EnqueueAsync<PayloadJob, ItemPayload>(payload, key, cancellationToken);

        Assert.Equal(first, await sut.EnqueueAsync<PayloadJob, ItemPayload>(payload with { }, key.ToUpperInvariant(), cancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EnqueueAsync<PayloadJob, ItemPayload>(payload with { Quantity = 5 }, key, cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.EnqueueAsync<PayloadJob, ItemPayload>(payload, "occurrence:2026-11-02T00:30+05:30", cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.EnqueueAsync<PayloadJob, ItemPayload>(payload, "someone@example.com", cancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey(), cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.EnqueueAsync<PayloadJob, ItemPayload>(payload with { }, new string('k', JobKeys.MaxLength + 1), cancellationToken));
    }

    [Fact]
    public async Task RunAllAsync_RecordedRun_RunsItAsTheSystemOnce()
    {
        var script = JobScript.Succeeding();
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<PayloadJob, ItemPayload>(JobsDatabase.Definition(JobsDatabase.NewName("in-process"))));
        var queue = new InProcessJobQueue(jobs.First.Services);
        var payload = new ItemPayload(Guid.CreateVersion7(), 7);
        var id = await queue.EnqueueAsync<PayloadJob, ItemPayload>(payload, JobsDatabase.NewKey(), TestContext.Current.CancellationToken);

        var ran = await queue.RunAllAsync(TestContext.Current.CancellationToken);
        var again = await queue.RunAllAsync(TestContext.Current.CancellationToken);

        var run = Assert.Single(ran);
        Assert.Equal(id, run.Id);
        Assert.Equal(payload, run.Payload);
        Assert.True(run.Result!.Value.IsSuccess);
        Assert.Empty(again);
        Assert.Equal(ActorIds.System, Assert.Single(script.Executions).ActorId);
        Assert.Equal(payload, Assert.Single(script.Payloads));
    }
}
