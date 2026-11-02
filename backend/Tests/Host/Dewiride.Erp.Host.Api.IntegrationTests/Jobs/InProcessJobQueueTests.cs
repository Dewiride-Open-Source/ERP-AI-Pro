using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Jobs;

public sealed class InProcessJobQueueTests
{
    private static readonly JobDefinition Definition = new("test.jobs.in-process-queue", "In-process test job", "Notes who it runs as for a test of the in-process queue.", JobRetryPolicy.Default, TimeSpan.FromMinutes(1));

    [Fact]
    public async Task WithInProcessJobQueue_EnqueueThenRunAll_KeepsTheRunInMemoryAndRunsItOnceAsTheSystem()
    {
        var actors = new ConcurrentQueue<Guid>();
        await using var factory = new ErpApiFactory()
            .WithInProcessJobQueue()
            .WithTestServices(services => services.AddSingleton(actors).AddJob<ActorRecordingJob>(Definition));
        var key = $"in-process:{Guid.CreateVersion7():N}";

        var (first, second) = (await QueueOfAScopeAsync(factory), await QueueOfAScopeAsync(factory));
        var queue = factory.Services.GetRequiredService<InProcessJobQueue>();
        await using var scope = factory.Services.CreateAsyncScope();
        var id = await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueAsync<ActorRecordingJob>(key, TestContext.Current.CancellationToken);
        var stored = await scope.ServiceProvider.GetRequiredService<JobsDbContext>().Runs.AnyAsync(r => r.IdempotencyKey == key, TestContext.Current.CancellationToken);
        var ran = await queue.RunAllAsync(TestContext.Current.CancellationToken);
        var again = await queue.RunAllAsync(TestContext.Current.CancellationToken);

        Assert.Same(queue, first);
        Assert.Same(queue, second);
        var recorded = Assert.Single(queue.Runs);
        Assert.Equal((id, Definition.Name, key), (recorded.Id, recorded.JobName, recorded.IdempotencyKey));
        Assert.False(stored);
        Assert.True(Assert.Single(ran, run => run.Id == id).Result!.Value.IsSuccess);
        Assert.Empty(again);
        Assert.Equal(ActorIds.System, Assert.Single(actors));
    }

    private static async Task<IJobQueue> QueueOfAScopeAsync(ErpApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return scope.ServiceProvider.GetRequiredService<IJobQueue>();
    }

    public sealed class ActorRecordingJob(IActorContext actor, ConcurrentQueue<Guid> actors) : IJob
    {
        public Task<Result> RunAsync(JobContext context, CancellationToken cancellationToken)
        {
            actors.Enqueue(actor.ActorId);
            return Task.FromResult(Result.Success());
        }
    }
}
