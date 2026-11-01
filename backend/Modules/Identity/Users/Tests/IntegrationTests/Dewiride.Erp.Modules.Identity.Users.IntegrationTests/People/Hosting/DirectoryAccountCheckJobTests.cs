using System.Net;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.People.Hosting;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Graph;
using Dewiride.Erp.Testing.Jobs;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Hosting;

// A test host runs no job loop, so each test enqueues a run of the check and runs it by its id, outside any request, as the
// runner runs the nightly one. Every test that closes an account also links one that stays enabled, because a check whose
// answer holds no enabled account changes nothing, and the test database may hold no other linked record when a test runs first.
public sealed class DirectoryAccountCheckJobTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task RunAsync_DisabledAccountInTheTestDirectory_DeactivatesThePersonAsTheErp()
    {
        using var client = Administrator(factory);
        var leaver = await RegisterLinkedAsync(factory, client, "Leaver");
        var stayer = await RegisterLinkedAsync(factory, client, "Stayer");
        AccountCheckApi.MarkAccount(factory, leaver.ObjectId, DirectoryAccountState.Disabled);

        var (_, outcome) = await RunCheckAsync(factory);

        Assert.Equal((JobRunStatus.Succeeded, (string?)null), (outcome.Status, outcome.ErrorCode));
        Assert.Equal((PersonStatus.Deactivated, PersonDeactivationReason.AccountDisabled), StateOf(await GetAsync(client, leaver.Person.Id)));
        Assert.Equal((PersonStatus.Active, (PersonDeactivationReason?)null), StateOf(await GetAsync(client, stayer.Person.Id)));
        var deactivation = (await ChangeRecordsApi.OfAsync(client, leaver.Person.Id))[^1];
        Assert.Equal((ChangeActorKind.System, ActorIds.System, (string?)null), (deactivation.ActorKind, deactivation.ActorId, deactivation.ActorName));
        Assert.Matches("^[0-9a-f]{32}$", outcome.TraceId);
        Assert.Equal(outcome.TraceId, deactivation.CorrelationId);
        Assert.Null(deactivation.ClientAddress);
        Assert.Equal(
            [
                new RecordedValue("deactivatedBecause", ChangeValueKind.Enum, null, "accountDisabled", null, null, null),
                new RecordedValue("status", ChangeValueKind.Enum, "active", "deactivated", null, null, null),
            ],
            deactivation.ValuesByProperty);
    }

    [Fact]
    public async Task RunAsync_DirectoryRefusingTheApplication_DeadLettersAtOnceWithAccessDeniedAndDeactivatesNobody()
    {
        await using var refusing = new ErpApiFactory();
        refusing.Services.GetRequiredService<TestDirectory>().RefuseApplicationTokens();
        using var client = Administrator(refusing);
        var person = await RegisterLinkedAsync(refusing, client, "Refused");
        AccountCheckApi.MarkAccount(refusing, person.ObjectId, DirectoryAccountState.Disabled);

        var (runId, outcome) = await RunCheckAsync(refusing);

        Assert.Equal((JobRunStatus.DeadLettered, DirectoryErrors.AccountsAccessDenied.Code), (outcome.Status, outcome.ErrorCode));
        var run = await RunOfAsync(refusing, runId);
        Assert.Equal((JobRunStatus.DeadLettered, 1, 1), (run.Status, run.AttemptCount, run.FailureCount));
        Assert.Equal((DirectoryErrors.AccountsAccessDenied.Code, DirectoryErrors.AccountsAccessDenied.Message), (run.LastErrorCode, run.LastErrorMessage));
        Assert.NotNull(run.FinishedAt);
        Assert.Equal(PersonStatus.Active, (await GetAsync(client, person.Person.Id)).Status);
    }

    private static async Task<(JobRunId RunId, JobRunOutcome Outcome)> RunCheckAsync(ErpApiFactory host)
    {
        JobRunId runId;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
            runId = await queue.EnqueueAsync<DirectoryAccountCheckJob>($"test:{Guid.CreateVersion7():N}", TestContext.Current.CancellationToken);
        }

        return (runId, await new JobRunnerDriver(host.Services).RunAsync(runId, TestContext.Current.CancellationToken));
    }

    private static async Task<JobRunDetails> RunOfAsync(ErpApiFactory host, JobRunId runId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var history = await scope.ServiceProvider.GetRequiredService<IJobAdministration>().GetRunAsync(runId, TestContext.Current.CancellationToken);
        Assert.True(history.IsSuccess);

        return history.Value.Run;
    }

    private static (PersonStatus Status, PersonDeactivationReason? Reason) StateOf(PersonResponse person) => (person.Status, person.DeactivatedBecause);

    private static async Task<PersonResponse> GetAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(PeopleApi.Path($"/{id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await PeopleApi.ReadPersonAsync(response);
    }

    private static async Task<(PersonResponse Person, Guid ObjectId)> RegisterLinkedAsync(ErpApiFactory host, HttpClient client, string name)
    {
        var registered = await PeopleApi.RegisterAsync(client, new { displayName = name, workEmail = PeopleApi.UniqueWorkEmail() });
        var objectId = await AccountCheckApi.LinkAsync(host, registered.Id);

        return (await GetAsync(client, registered.Id), objectId);
    }

    private static HttpClient Administrator(ErpApiFactory host) => host.CreateClient().AsUser(TestUsers.Administrator);
}
