using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.ListJobRuns;

namespace Dewiride.Erp.Modules.Platform.Jobs.UnitTests.Runs.Application.Queries.ListJobRuns;

public sealed class ListJobRunsHandlerTests
{
    [Fact]
    public async Task HandleAsync_Request_HandsItToTheAdministrationAndReturnsItsPage()
    {
        var page = new PagedResult<JobRunDetails>([JobRunSamples.Run(JobRunStatus.DeadLettered)], 2, 10, 11);
        var administration = new RecordingJobAdministration { Runs = page };
        var request = ListRequest.Parse(2, 10, "dueAt:asc", "status:eq:deadLettered").Value;

        var result = await new ListJobRunsHandler(administration).HandleAsync(new ListJobRunsQuery(request), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(page, result.Value);
        Assert.Equal([nameof(IJobAdministration.ListRunsAsync)], administration.Calls);
        Assert.Same(request, administration.ReceivedRequest);
        Assert.Equal(TestContext.Current.CancellationToken, administration.ReceivedCancellation);
    }

    [Fact]
    public async Task HandleAsync_RequestTheAdministrationRefuses_ReturnsTheRefusal()
    {
        var refusal = QueryErrors.Field("runs", ["createdAt", "dueAt", "finishedAt"]);
        var administration = new RecordingJobAdministration { Runs = refusal };

        var result = await new ListJobRunsHandler(administration).HandleAsync(new ListJobRunsQuery(ListRequest.Default), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(refusal, result.Error);
    }
}
