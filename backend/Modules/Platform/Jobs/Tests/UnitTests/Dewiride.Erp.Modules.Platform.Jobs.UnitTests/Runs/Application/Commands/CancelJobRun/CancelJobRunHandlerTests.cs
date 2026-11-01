using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Commands.CancelJobRun;

namespace Dewiride.Erp.Modules.Platform.Jobs.UnitTests.Runs.Application.Commands.CancelJobRun;

public sealed class CancelJobRunHandlerTests
{
    [Fact]
    public async Task HandleAsync_WaitingRun_ReturnsTheRunAsTheAdministrationCancelledIt()
    {
        var cancelled = JobRunSamples.Run(JobRunStatus.Cancelled);
        var administration = new RecordingJobAdministration { Cancel = cancelled };

        var result = await new CancelJobRunHandler(administration).HandleAsync(new CancelJobRunCommand(cancelled.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(cancelled, result.Value);
        Assert.Equal([nameof(IJobAdministration.CancelAsync)], administration.Calls);
        Assert.Equal(cancelled.Id, administration.ReceivedId);
        Assert.Equal(TestContext.Current.CancellationToken, administration.ReceivedCancellation);
    }

    [Theory]
    [InlineData("job.run-not-found")]
    [InlineData("job.run-finished")]
    public async Task HandleAsync_RunTheAdministrationRefuses_ReturnsTheRefusal(string code)
    {
        var refusal = new[] { JobErrors.RunNotFound, JobErrors.RunFinished }.Single(error => error.Code == code);
        var administration = new RecordingJobAdministration { Cancel = refusal };

        var result = await new CancelJobRunHandler(administration).HandleAsync(new CancelJobRunCommand(JobRunId.Create()), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(refusal, result.Error);
    }
}
