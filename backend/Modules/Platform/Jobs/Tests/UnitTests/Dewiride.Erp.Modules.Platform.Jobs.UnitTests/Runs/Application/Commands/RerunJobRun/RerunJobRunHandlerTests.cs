using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Commands.RerunJobRun;

namespace Dewiride.Erp.Modules.Platform.Jobs.UnitTests.Runs.Application.Commands.RerunJobRun;

public sealed class RerunJobRunHandlerTests
{
    [Fact]
    public async Task HandleAsync_RunThatGaveUp_ReturnsTheRunAsTheAdministrationPutItBack()
    {
        var waiting = JobRunSamples.Run(JobRunStatus.Pending);
        var administration = new RecordingJobAdministration { Rerun = waiting };

        var result = await new RerunJobRunHandler(administration).HandleAsync(new RerunJobRunCommand(waiting.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(waiting, result.Value);
        Assert.Equal([nameof(IJobAdministration.RerunAsync)], administration.Calls);
        Assert.Equal(waiting.Id, administration.ReceivedId);
        Assert.Equal(TestContext.Current.CancellationToken, administration.ReceivedCancellation);
    }

    [Theory]
    [InlineData("job.run-not-found")]
    [InlineData("job.run-not-rerunnable")]
    [InlineData("job.not-defined")]
    public async Task HandleAsync_RunTheAdministrationRefuses_ReturnsTheRefusal(string code)
    {
        var refusal = new[] { JobErrors.RunNotFound, JobErrors.RunNotRerunnable, JobErrors.JobNotDefined }.Single(error => error.Code == code);
        var administration = new RecordingJobAdministration { Rerun = refusal };

        var result = await new RerunJobRunHandler(administration).HandleAsync(new RerunJobRunCommand(JobRunId.Create()), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(refusal, result.Error);
    }
}
