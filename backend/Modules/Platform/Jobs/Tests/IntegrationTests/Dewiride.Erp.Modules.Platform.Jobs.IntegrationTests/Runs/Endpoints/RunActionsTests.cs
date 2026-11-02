using System.Net;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints.TestJobs;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints.Responses;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Jobs;

namespace Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints;

public sealed class RunActionsTests(JobsHost host) : IClassFixture<JobsHost>
{
    [Fact]
    public async Task Post_RerunOfARunThatGaveUp_PutsItBackDueAtOnceWithItsFailuresCountedFromZeroAndNamesTheAdministratorWhoAsked()
    {
        var person = host.AdmitAdministrator("Kavya Menon");
        using var session = TestSignIn.CreateClient(host.Factory);
        using var signIn = await TestSignIn.SignInAsync(session, person);
        var id = await SeedAsync(JobRunStatus.DeadLettered, attempts: 2);
        var before = host.Clock.GetUtcNow();

        using var response = await JobsApi.PostAsync(session, id, JobsApi.Rerun);

        var run = await JobsApi.ReadRunAsync(response);
        Assert.Equal((id, JobRunStatus.Pending, 2, 0), (run.Id, run.Status, run.AttemptCount, run.FailureCount));
        Assert.InRange(run.DueAt, before, host.Clock.GetUtcNow());
        Assert.Null(run.FinishedAt);
        Assert.Null(run.LastErrorCode);
        Assert.Null(run.LastErrorMessage);
        using var administrator = host.Administrator();
        var rerun = (await JobsApi.GetRunAsync(administrator, id)).Events[^1];
        Assert.Equal(JobRunEventKind.Rerun, rerun.Kind);
        Assert.Equal(new JobActorResponse(JobActorKind.Person, person.ObjectId, person.Name), rerun.Actor);
        Assert.Null(rerun.Attempt);
    }

    [Theory]
    [InlineData(JobRunStatus.Failed)]
    [InlineData(JobRunStatus.Cancelled)]
    public async Task Post_RerunOfAFailedOrCancelledRun_PutsItBackAsWaitingWithNoCancelLeft(JobRunStatus status)
    {
        using var client = host.Administrator();
        var id = await SeedAsync(status);

        using var response = await JobsApi.PostAsync(client, id, JobsApi.Rerun);

        var run = await JobsApi.ReadRunAsync(response);
        Assert.Equal((JobRunStatus.Pending, 0), (run.Status, run.FailureCount));
        Assert.Null(run.CancelRequestedAt);
        Assert.Null(run.FinishedAt);
    }

    [Theory]
    [InlineData(JobRunStatus.Pending)]
    [InlineData(JobRunStatus.Running)]
    [InlineData(JobRunStatus.Succeeded)]
    public async Task Post_RerunOfARunThatNeitherFailedNorGaveUpNorWasCancelled_AnswersRunNotRerunnable(JobRunStatus status)
    {
        using var client = host.Administrator();
        var id = await SeedAsync(status);

        using var response = await JobsApi.PostAsync(client, id, JobsApi.Rerun);

        await JobsApi.AssertProblemAsync(response, HttpStatusCode.Conflict, JobErrors.RunNotRerunnable.Code);
        Assert.Equal(status, (await JobsApi.GetRunAsync(client, id)).Run.Status);
    }

    [Fact]
    public async Task Post_RerunTwice_RefusesTheSecondBecauseTheRunWaitsAlready()
    {
        using var client = host.Administrator();
        var id = await SeedAsync(JobRunStatus.DeadLettered);
        using (var first = await JobsApi.PostAsync(client, id, JobsApi.Rerun))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var second = await JobsApi.PostAsync(client, id, JobsApi.Rerun);

        await JobsApi.AssertProblemAsync(second, HttpStatusCode.Conflict, JobErrors.RunNotRerunnable.Code);
        Assert.Single((await JobsApi.GetRunAsync(client, id)).Events, step => step.Kind == JobRunEventKind.Rerun);
    }

    [Fact]
    public async Task Post_RerunOfARunOfAJobNoHostDefines_AnswersJobNotDefined()
    {
        using var client = host.Administrator();
        var id = await JobsApi.SeedAsync(host.Factory.Services, new JobRunSeed(TestJobDefinitions.RetiredJobName, NewKey(), JobRunStatus.DeadLettered));

        using var response = await JobsApi.PostAsync(client, id, JobsApi.Rerun);

        await JobsApi.AssertProblemAsync(response, HttpStatusCode.Conflict, JobErrors.JobNotDefined.Code);
    }

    [Theory]
    [InlineData(JobRunStatus.Pending)]
    [InlineData(JobRunStatus.Failed)]
    public async Task Post_CancelOfAWaitingRun_CancelsItAtOnceAndNamesTheAdministratorWhoAsked(JobRunStatus status)
    {
        using var client = host.Administrator();
        var id = await SeedAsync(status);
        var before = host.Clock.GetUtcNow();

        using var response = await JobsApi.PostAsync(client, id, JobsApi.Cancel);

        var run = await JobsApi.ReadRunAsync(response);
        Assert.Equal(JobRunStatus.Cancelled, run.Status);
        var finishedAt = Assert.NotNull(run.FinishedAt);
        Assert.InRange(finishedAt, before, host.Clock.GetUtcNow());
        Assert.Equal(run.FinishedAt, run.CancelRequestedAt);
        var cancelled = (await JobsApi.GetRunAsync(client, id)).Events[^1];
        Assert.Equal(JobRunEventKind.Cancelled, cancelled.Kind);
        Assert.Equal((JobActorKind.Person, TestUsers.Administrator.ObjectId), (cancelled.Actor.Kind, cancelled.Actor.Id));
    }

    [Fact]
    public async Task Post_CancelOfARunningRun_AsksItToStopAndAnswersTheSameWhenAskedAgain()
    {
        using var client = host.Administrator();
        var id = await SeedAsync(JobRunStatus.Running);

        using var first = await JobsApi.PostAsync(client, id, JobsApi.Cancel);
        var asked = await JobsApi.ReadRunAsync(first);
        using var second = await JobsApi.PostAsync(client, id, JobsApi.Cancel);
        var again = await JobsApi.ReadRunAsync(second);

        Assert.Equal(JobRunStatus.Running, asked.Status);
        Assert.NotNull(asked.CancelRequestedAt);
        Assert.Null(asked.FinishedAt);
        Assert.Equal(asked, again);
        var request = Assert.Single((await JobsApi.GetRunAsync(client, id)).Events, step => step.Kind == JobRunEventKind.CancelRequested);
        Assert.Equal((JobActorKind.Person, TestUsers.Administrator.ObjectId), (request.Actor.Kind, request.Actor.Id));
    }

    [Theory]
    [InlineData(JobRunStatus.Succeeded)]
    [InlineData(JobRunStatus.DeadLettered)]
    [InlineData(JobRunStatus.Cancelled)]
    public async Task Post_CancelOfAFinishedRun_AnswersRunFinished(JobRunStatus status)
    {
        using var client = host.Administrator();
        var id = await SeedAsync(status);

        using var response = await JobsApi.PostAsync(client, id, JobsApi.Cancel);

        await JobsApi.AssertProblemAsync(response, HttpStatusCode.Conflict, JobErrors.RunFinished.Code);
        Assert.Equal(status, (await JobsApi.GetRunAsync(client, id)).Run.Status);
    }

    [Theory]
    [InlineData(JobsApi.Rerun)]
    [InlineData(JobsApi.Cancel)]
    public async Task Post_ActionOnAnUnknownRun_AnswersRunNotFound(string action)
    {
        using var client = host.Administrator();

        using var response = await JobsApi.PostAsync(client, Guid.CreateVersion7(), action);

        await JobsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, JobErrors.RunNotFound.Code);
    }

    private static string NewKey() => $"action:{JobsApi.NewMarker()}";

    private Task<Guid> SeedAsync(JobRunStatus status, int attempts = 1) =>
        JobsApi.SeedAsync(host.Factory.Services, new JobRunSeed(TestJobDefinitions.Succeeding.Name, NewKey(), status) { Attempts = attempts });
}
