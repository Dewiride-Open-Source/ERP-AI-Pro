using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.GetJobRun;

namespace Dewiride.Erp.Modules.Platform.Jobs.UnitTests.Runs.Application.Queries.GetJobRun;

public sealed class GetJobRunHandlerTests
{
    [Fact]
    public async Task HandleAsync_KnownRun_ReturnsItsHistoryFromTheAdministration()
    {
        var run = JobRunSamples.Run(JobRunStatus.Succeeded);
        var system = new JobActorDetails(JobActorKind.System, ActorIds.System, null);
        var history = new JobRunHistory(
            run,
            [
                new JobRunEventDetails(Guid.CreateVersion7(), JobRunSamples.OccurrenceAt, JobRunEventKind.Enqueued, system, null, null, null, null, null, null),
                new JobRunEventDetails(Guid.CreateVersion7(), JobRunSamples.OccurrenceAt, JobRunEventKind.Started, system, 1, "erp-host:4242", "4bf92f3577b34da6a3ce929d0e0e4736", null, null, null),
                new JobRunEventDetails(Guid.CreateVersion7(), JobRunSamples.OccurrenceAt, JobRunEventKind.Succeeded, system, 1, "erp-host:4242", "4bf92f3577b34da6a3ce929d0e0e4736", null, null, null),
            ]);
        var administration = new RecordingJobAdministration { History = history };

        var result = await new GetJobRunHandler(administration).HandleAsync(new GetJobRunQuery(run.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(history, result.Value);
        Assert.Equal([nameof(IJobAdministration.GetRunAsync)], administration.Calls);
        Assert.Equal(run.Id, administration.ReceivedId);
        Assert.Equal(TestContext.Current.CancellationToken, administration.ReceivedCancellation);
    }

    [Fact]
    public async Task HandleAsync_UnknownRun_ReturnsRunNotFound()
    {
        var administration = new RecordingJobAdministration { History = JobErrors.RunNotFound };
        var id = JobRunId.Create();

        var result = await new GetJobRunHandler(administration).HandleAsync(new GetJobRunQuery(id), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(JobErrors.RunNotFound, result.Error);
        Assert.Equal(id, administration.ReceivedId);
    }
}
