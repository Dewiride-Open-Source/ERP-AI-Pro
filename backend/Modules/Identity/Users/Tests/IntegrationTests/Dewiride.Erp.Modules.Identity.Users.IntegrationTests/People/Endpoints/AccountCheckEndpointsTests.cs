using System.Net;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Graph;
using Microsoft.AspNetCore.TestHost;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;

// Every test that closes accounts also links one that stays enabled, because a check whose answer holds no enabled account
// changes nothing, and the test database may hold no other linked record when a test runs first.
public sealed class AccountCheckEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task CheckDirectoryAccounts_DisabledAndDeletedAccounts_DeactivatesTheirPeopleWithTheReasonAndLeavesTheOthers()
    {
        using var client = Administrator();
        var disabled = await RegisterLinkedAsync(client, "Disabled");
        var deleted = await RegisterLinkedAsync(client, "Deleted");
        var enabled = await RegisterLinkedAsync(client, "Enabled");
        AccountCheckApi.MarkAccount(factory, disabled.ObjectId, DirectoryAccountState.Disabled);
        AccountCheckApi.MarkAccount(factory, deleted.ObjectId, DirectoryAccountState.Deleted);

        var check = await AccountCheckApi.CheckAsync(client);

        Assert.True(check.CheckedCount >= 3);
        Assert.Contains(new DeactivatedPersonResponse(disabled.Person.Id, disabled.Person.DisplayName, PersonDeactivationReason.AccountDisabled), check.Deactivated);
        Assert.Contains(new DeactivatedPersonResponse(deleted.Person.Id, deleted.Person.DisplayName, PersonDeactivationReason.AccountDeleted), check.Deactivated);
        Assert.DoesNotContain(check.Deactivated, person => person.Id == enabled.Person.Id);
        Assert.Equal((PersonStatus.Deactivated, PersonDeactivationReason.AccountDisabled), StateOf(await GetAsync(client, disabled.Person.Id)));
        Assert.Equal((PersonStatus.Deactivated, PersonDeactivationReason.AccountDeleted), StateOf(await GetAsync(client, deleted.Person.Id)));
        Assert.Equal((PersonStatus.Active, (PersonDeactivationReason?)null), StateOf(await GetAsync(client, enabled.Person.Id)));
    }

    [Fact]
    public async Task CheckDirectoryAccounts_SeveralPeopleDeactivated_ListsThemByDisplayName()
    {
        using var client = Administrator();
        var zara = await RegisterLinkedAsync(client, $"Zara {Guid.CreateVersion7():N}");
        var aditi = await RegisterLinkedAsync(client, $"Aditi {Guid.CreateVersion7():N}");
        await RegisterLinkedAsync(client, "Enabled");
        AccountCheckApi.MarkAccount(factory, zara.ObjectId, DirectoryAccountState.Disabled);
        AccountCheckApi.MarkAccount(factory, aditi.ObjectId, DirectoryAccountState.Disabled);

        var check = await AccountCheckApi.CheckAsync(client);

        Assert.Equal([aditi.Person.Id, zara.Person.Id], check.Deactivated.Select(person => person.Id).Where(id => id == aditi.Person.Id || id == zara.Person.Id));
    }

    [Fact]
    public async Task CheckDirectoryAccounts_PersonDeactivatedAlready_KeepsTheirReasonAndDoesNotListThem()
    {
        using var client = Administrator();
        var person = await RegisterLinkedAsync(client, "Left Earlier");
        await RegisterLinkedAsync(client, "Enabled");
        var deactivated = await ChangeStatusAsync(client, person.Person, "deactivated");
        AccountCheckApi.MarkAccount(factory, person.ObjectId, DirectoryAccountState.Disabled);

        var check = await AccountCheckApi.CheckAsync(client);

        Assert.DoesNotContain(check.Deactivated, listed => listed.Id == person.Person.Id);
        var read = await GetAsync(client, person.Person.Id);
        Assert.Equal((PersonStatus.Deactivated, PersonDeactivationReason.Administrator), StateOf(read));
        Assert.Equal(deactivated.Version, read.Version);
    }

    [Fact]
    public async Task CheckDirectoryAccounts_RecordLinkedToNoAccount_LeavesItActive()
    {
        using var client = Administrator();
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await RegisterLinkedAsync(client, "Enabled");

        var check = await AccountCheckApi.CheckAsync(client);

        Assert.DoesNotContain(check.Deactivated, listed => listed.Id == registered.Id);
        Assert.Equal(PersonStatus.Active, (await GetAsync(client, registered.Id)).Status);
    }

    [Fact]
    public async Task CheckDirectoryAccounts_ManagerWhoseAccountIsDisabled_KeepsTheirTeamWhoseRecordsSayTheirManagerIsInactive()
    {
        using var client = Administrator();
        var manager = await RegisterLinkedAsync(client, "Lead");
        await RegisterLinkedAsync(client, "Enabled");
        var report = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, report.Id, OrganisationApi.Today.AddDays(-30), managerId: manager.Person.Id);
        Assert.True((await GetAsync(client, report.Id)).ManagerActive);
        AccountCheckApi.MarkAccount(factory, manager.ObjectId, DirectoryAccountState.Disabled);

        await AccountCheckApi.CheckAsync(client);

        var read = await GetAsync(client, report.Id);
        Assert.Equal(manager.Person.Id, read.ManagerId);
        Assert.False(read.ManagerActive);
        var listed = await OrganisationApi.ReadPageAsync<PersonResponse>(client, PeopleApi.Path($"?filter={Uri.EscapeDataString($"managerId:eq:{manager.Person.Id}")}"));
        Assert.False(Assert.Single(listed.Items).ManagerActive);
    }

    [Fact]
    public async Task CheckDirectoryAccounts_ThenReactivation_ClearsTheReasonAndNeedsNoHandoverOfTheTeam()
    {
        using var client = Administrator();
        var manager = await RegisterLinkedAsync(client, "Lead");
        await RegisterLinkedAsync(client, "Enabled");
        var report = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, report.Id, OrganisationApi.Today.AddDays(-30), managerId: manager.Person.Id);
        AccountCheckApi.MarkAccount(factory, manager.ObjectId, DirectoryAccountState.Deleted);
        await AccountCheckApi.CheckAsync(client);
        AccountCheckApi.MarkAccount(factory, manager.ObjectId, DirectoryAccountState.Enabled);

        var reactivated = await ChangeStatusAsync(client, await GetAsync(client, manager.Person.Id), "active");

        Assert.Equal((PersonStatus.Active, (PersonDeactivationReason?)null), StateOf(reactivated));
        Assert.True((await GetAsync(client, report.Id)).ManagerActive);
    }

    [Fact]
    public async Task CheckDirectoryAccounts_DeactivatedPerson_RecordsTheReasonInTheirChangeRecords()
    {
        using var client = Administrator();
        var person = await RegisterLinkedAsync(client, "Leaver");
        await RegisterLinkedAsync(client, "Enabled");
        AccountCheckApi.MarkAccount(factory, person.ObjectId, DirectoryAccountState.Disabled);

        await AccountCheckApi.CheckAsync(client);

        var deactivation = (await ChangeRecordsApi.OfAsync(client, person.Person.Id))[^1];
        Assert.Equal(TestUsers.Administrator.ObjectId, deactivation.ActorId);
        Assert.Equal(
            [
                new RecordedValue("deactivatedBecause", ChangeValueKind.Enum, null, "accountDisabled", null, null, null),
                new RecordedValue("status", ChangeValueKind.Enum, "active", "deactivated", null, null, null),
            ],
            deactivation.ValuesByProperty);
    }

    [Fact]
    public async Task CheckDirectoryAccounts_Always_ReadsTheAccountsWithAnApplicationTokenOfMicrosoftGraph()
    {
        using var client = Administrator();
        var person = await RegisterLinkedAsync(client, "Checked");
        var directory = factory.Services.GetRequiredService<TestDirectory>();
        var tokenEndpoint = factory.Services.GetRequiredService<TestTokenEndpoint>();

        await AccountCheckApi.CheckAsync(client);

        var request = directory.Requests.Last(r => r.Address.Query.Contains(person.ObjectId.ToString("D"), StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(request.AccessToken);
        Assert.True(tokenEndpoint.IsApplicationToken(request.AccessToken));
        Assert.Equal(TestTokenEndpoint.ApplicationScope, tokenEndpoint.ScopeOf(request.AccessToken));
        Assert.Contains(tokenEndpoint.Requests, token => token.Form.GetValueOrDefault("grant_type") == "client_credentials" && token.Form.GetValueOrDefault("scope") == TestTokenEndpoint.ApplicationScope);
    }

    [Fact]
    public async Task CheckDirectoryAccounts_DirectoryRefusingTheApplication_Returns403AccessDeniedAndDeactivatesNobody()
    {
        await using var refusing = new ErpApiFactory();
        refusing.Services.GetRequiredService<TestDirectory>().RefuseApplicationTokens();
        using var client = refusing.CreateClient().AsUser(TestUsers.Administrator);
        var person = await RegisterLinkedAsync(refusing, client, "Refused");
        AccountCheckApi.MarkAccount(refusing, person.ObjectId, DirectoryAccountState.Disabled);

        using var response = await AccountCheckApi.PostAsync(client);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("directory.access-denied", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        Assert.Equal(PersonStatus.Active, (await GetAsync(client, person.Person.Id)).Status);
    }

    [Fact]
    public async Task CheckDirectoryAccounts_DirectoryAnsweringNoEnabledAccount_Returns503AndDeactivatesNobody()
    {
        await using var silent = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddScoped<IDirectoryAccounts, NoEnabledAccount>()));
        using var client = silent.CreateClient().AsUser(TestUsers.Administrator);
        var person = await RegisterLinkedAsync(client, "Kept");

        using var response = await AccountCheckApi.PostAsync(client);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("directory.unavailable", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        Assert.Equal(PersonStatus.Active, (await GetAsync(client, person.Person.Id)).Status);
    }

    [Fact]
    public async Task CheckDirectoryAccounts_ByAnAdministratorWhoseOwnRecordIsDeactivated_Returns409ActorInactiveAndDeactivatesNobody()
    {
        var administrator = NewAdministrator();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var record = User.Invite(administrator.ObjectId, administrator.Name, administrator.UserName, null, null, null, []).Value;
            record.Deactivate(DeactivationReason.Administrator);
            context.Users.Add(record);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = factory.CreateClient().AsUser(administrator);
        using var other = Administrator();
        var person = await RegisterLinkedAsync(other, "Kept");
        await RegisterLinkedAsync(other, "Enabled");
        AccountCheckApi.MarkAccount(factory, person.ObjectId, DirectoryAccountState.Disabled);

        using var response = await AccountCheckApi.PostAsync(client);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.actor-inactive", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        Assert.Equal(PersonStatus.Active, (await GetAsync(other, person.Person.Id)).Status);
        AccountCheckApi.MarkAccount(factory, person.ObjectId, DirectoryAccountState.Enabled);
    }

    [Theory]
    [InlineData(DirectoryAccountState.Disabled)]
    [InlineData(DirectoryAccountState.Deleted)]
    public async Task CheckDirectoryAccounts_AnswerWithoutTheEnabledAccountOfTheAdministratorRunningIt_Returns503AndDeactivatesNobody(DirectoryAccountState own)
    {
        var administrator = NewAdministrator();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            context.Users.Add(User.Invite(administrator.ObjectId, administrator.Name, administrator.UserName, null, null, null, []).Value);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = factory.CreateClient().AsUser(administrator);
        using var other = Administrator();
        var person = await RegisterLinkedAsync(other, "Kept");
        await RegisterLinkedAsync(other, "Enabled");
        AccountCheckApi.MarkAccount(factory, person.ObjectId, DirectoryAccountState.Disabled);
        AccountCheckApi.MarkAccount(factory, administrator.ObjectId, own);
        try
        {
            using var response = await AccountCheckApi.PostAsync(client);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("directory.unavailable", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
            Assert.Equal(PersonStatus.Active, (await GetAsync(other, person.Person.Id)).Status);
        }
        finally
        {
            AccountCheckApi.MarkAccount(factory, person.ObjectId, DirectoryAccountState.Enabled);
            AccountCheckApi.MarkAccount(factory, administrator.ObjectId, DirectoryAccountState.Enabled);
        }
    }

    [Fact]
    public async Task CheckDirectoryAccounts_AsPersonWithoutThePermissionToChangeStatus_Returns403()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await AccountCheckApi.PostAsync(client);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("request.forbidden", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    private static (PersonStatus Status, PersonDeactivationReason? Reason) StateOf(PersonResponse person) => (person.Status, person.DeactivatedBecause);

    private static async Task<PersonResponse> GetAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(PeopleApi.Path($"/{id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await PeopleApi.ReadPersonAsync(response);
    }

    private static async Task<PersonResponse> ChangeStatusAsync(HttpClient client, PersonResponse person, string status)
    {
        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{person.Id}/status"), new { status, version = person.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await PeopleApi.ReadPersonAsync(response);
    }

    private static async Task<(PersonResponse Person, Guid ObjectId)> RegisterLinkedAsync(ErpApiFactory host, HttpClient client, string name)
    {
        var registered = await PeopleApi.RegisterAsync(client, new { displayName = name, workEmail = PeopleApi.UniqueWorkEmail() });
        var objectId = await AccountCheckApi.LinkAsync(host, registered.Id);

        return (await GetAsync(client, registered.Id), objectId);
    }

    private Task<(PersonResponse Person, Guid ObjectId)> RegisterLinkedAsync(HttpClient client, string name) => RegisterLinkedAsync(factory, client, name);

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);

    private TestUser NewAdministrator()
    {
        var objectId = Guid.CreateVersion7();
        var administrator = new TestUser(objectId, "Vikram Iyer", $"vikram.iyer.{objectId:N}@dewiride.test", [AppRoles.User, AppRoles.Administrator]);
        factory.Services.GetRequiredService<TestTokenEndpoint>().Admit(administrator);

        return administrator;
    }

    private sealed class NoEnabledAccount : IDirectoryAccounts
    {
        public Task<Result<IReadOnlyDictionary<Guid, DirectoryAccountState>>> StatesOfAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyDictionary<Guid, DirectoryAccountState>>(objectIds.ToDictionary(objectId => objectId, _ => DirectoryAccountState.Disabled)));
    }
}
