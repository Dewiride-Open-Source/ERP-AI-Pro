using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Origins;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Jobs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Jobs;

// The two hosts share the test database and the looping one claims every due run of a job it defines, so each defines its job
// under a name of its own.
public sealed class JobRunnerCompositionTests(JobRunnerCompositionTests.Fixture fixture) : IClassFixture<JobRunnerCompositionTests.Fixture>
{
    private static readonly TimeSpan LoopTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public void Services_Api_RegisterTheRunnerAndTheFactoryRemovesItsLoop()
    {
        var services = fixture.Driven.Services;

        Assert.NotNull(services.GetService<JobRunner>());
        Assert.NotNull(services.GetService<JobScheduler>());
        Assert.NotNull(services.GetService<JobInvoker>());
        Assert.NotNull(services.GetService<JobRunnerInstance>());
        Assert.DoesNotContain(services.GetServices<IHostedService>(), service => service is JobRunnerService);
        Assert.Single(fixture.Looping.Services.GetServices<IHostedService>(), service => service is JobRunnerService);
    }

    [Fact]
    public async Task Services_WithJobRunner_RunTheLoop()
    {
        var services = fixture.Looping.Services;
        var runId = await EnqueueAsync(services);

        var attempt = await services.GetRequiredService<Observations>().OfAsync(runId).WaitAsync(LoopTimeout, TestContext.Current.CancellationToken);
        await services.GetRequiredService<JobRunner>().WhenIdleAsync();

        var history = await HistoryAsync(services, runId);
        Assert.Equal(JobRunStatus.Succeeded, history.Run.Status);
        var started = Assert.Single(history.Events, step => step.Kind == JobRunEventKind.Started);
        Assert.Equal(new ObservedAttempt(ActorIds.System, false, null, started.TraceId), attempt);
    }

    [Fact]
    public async Task RunAsync_InTheApiHost_RunsAsTheSystemWithTheAttemptsTraceAsCorrelation()
    {
        var services = fixture.Driven.Services;
        var runId = await EnqueueAsync(services);

        var outcome = await new JobRunnerDriver(services).RunAsync(runId, TestContext.Current.CancellationToken);

        Assert.Equal((JobRunStatus.Succeeded, (string?)null), (outcome.Status, outcome.ErrorCode));
        Assert.Matches("^[0-9a-f]{32}$", outcome.TraceId);
        var attempt = await services.GetRequiredService<Observations>().OfAsync(runId).WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new ObservedAttempt(ActorIds.System, false, null, outcome.TraceId), attempt);
    }

    private static async Task<JobRunId> EnqueueAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueAsync<ObservingJob>($"composition:{Guid.CreateVersion7():N}", TestContext.Current.CancellationToken);
    }

    private static async Task<JobRunHistory> HistoryAsync(IServiceProvider services, JobRunId runId)
    {
        await using var scope = services.CreateAsyncScope();
        var history = await scope.ServiceProvider.GetRequiredService<IJobAdministration>().GetRunAsync(runId, TestContext.Current.CancellationToken);
        Assert.True(history.IsSuccess);

        return history.Value;
    }

    public sealed record ObservedAttempt(Guid ActorId, bool IsAuthenticated, string? ClientAddress, string? CorrelationId);

    public sealed class Observations
    {
        private readonly ConcurrentDictionary<JobRunId, TaskCompletionSource<ObservedAttempt>> _attempts = new();

        public Task<ObservedAttempt> OfAsync(JobRunId runId) => Completion(runId).Task;

        public void Record(JobRunId runId, ObservedAttempt attempt) => Completion(runId).TrySetResult(attempt);

        private TaskCompletionSource<ObservedAttempt> Completion(JobRunId runId) =>
            _attempts.GetOrAdd(runId, _ => new TaskCompletionSource<ObservedAttempt>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    public sealed class ObservingJob(IActorContext actor, IRequestOrigin origin, Observations observations) : IJob
    {
        public Task<Result> RunAsync(JobContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            observations.Record(context.RunId, new ObservedAttempt(actor.ActorId, actor.IsAuthenticated, origin.ClientAddress, origin.CorrelationId));
            return Task.FromResult(Result.Success());
        }
    }

    public sealed class Fixture : IAsyncDisposable
    {
        private static readonly JobDefinition DrivenJob = new("test.jobs.composition-driven", "Driven test job", "Notes who it runs as for a test that runs it by hand.", JobRetryPolicy.Default, TimeSpan.FromMinutes(1));

        private static readonly JobDefinition LoopedJob = new("test.jobs.composition-looped", "Looped test job", "Notes who it runs as for a test that leaves it to the loop.", JobRetryPolicy.Default, TimeSpan.FromMinutes(1));

        private readonly ErpApiFactory _driven = new();

        private readonly ErpApiFactory _looping = new ErpApiFactory().WithJobRunner();

        public Fixture()
        {
            Driven = WithObservingJob(_driven, DrivenJob);
            Looping = WithObservingJob(_looping, LoopedJob);
        }

        public WebApplicationFactory<Program> Driven { get; }

        public WebApplicationFactory<Program> Looping { get; }

        public async ValueTask DisposeAsync()
        {
            await Looping.DisposeAsync();
            await _looping.DisposeAsync();
            await Driven.DisposeAsync();
            await _driven.DisposeAsync();
        }

        private static WebApplicationFactory<Program> WithObservingJob(ErpApiFactory factory, JobDefinition definition) =>
            factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<Observations>().AddJob<ObservingJob>(definition)));
    }
}
