using System.Net;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Integration;

// Each call runs in a scope of the test host's services outside any request, as work that no request drives does, so its
// changes are made as the system actor; the test reads them back through the routes as an administrator.
public sealed class PeopleCommandsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task InvitePersonAsync_PersonOfTheDirectory_CreatesAnActiveRecordLinkedToTheirAccountWithTheirRoles()
    {
        var objectId = Guid.CreateVersion7();
        var signInName = $"Meera.Nair.{objectId:N}@Dewiride.test";
        var employeeCode = PeopleApi.UniqueEmployeeCode();

        var invited = await InviteAsync(new PersonInvitation(objectId, "Meera Nair", signInName, employeeCode, [PersonRole.Manager, PersonRole.Hr]));

        Assert.True(invited.IsSuccess);
        var person = await GetAsync(invited.Value);
        Assert.Equal(objectId, person.EntraObjectId);
        Assert.Equal("Meera Nair", person.DisplayName);
        Assert.Equal(signInName.ToLowerInvariant(), person.WorkEmail);
        Assert.Equal(employeeCode, person.EmployeeCode);
        Assert.Equal(PersonStatus.Active, person.Status);
        Assert.Equal([PersonRole.Hr, PersonRole.Manager], person.Roles);
    }

    [Fact]
    public async Task InvitePersonAsync_ObjectIdARecordHolds_AnswersEntraObjectIdTakenAsTheInvitationRouteDoes()
    {
        var objectId = Guid.CreateVersion7();
        var invitation = new PersonInvitation(objectId, "Meera Nair", $"meera.{objectId:N}@dewiride.test", null, []);
        Assert.True((await InviteAsync(invitation)).IsSuccess);

        var again = await InviteAsync(invitation with { SignInName = $"other.{objectId:N}@dewiride.test" });

        Assert.Equal("user.entra-object-id-taken", again.Error?.Code);
        Assert.Equal(ErrorKind.Conflict, again.Error?.Kind);
        Assert.Equal(1, await RecordsOfAsync(objectId));
    }

    [Fact]
    public async Task InvitePersonAsync_SignInNameThatIsNoEmailAddress_AnswersWorkEmailInvalidAndCreatesNoRecord()
    {
        var objectId = Guid.CreateVersion7();

        var refused = await InviteAsync(new PersonInvitation(objectId, "Meera Nair", "not an email", null, []));

        Assert.Equal("user.work-email-invalid", refused.Error?.Code);
        Assert.Equal(ErrorKind.Validation, refused.Error?.Kind);
        Assert.Equal(0, await RecordsOfAsync(objectId));
    }

    [Fact]
    public async Task InvitePersonAsync_OutsideARequest_RecordsTheCreationAsMadeByTheSystemActorItRunsUnder()
    {
        var objectId = Guid.CreateVersion7();

        var invited = await InviteAsync(new PersonInvitation(objectId, "Meera Nair", $"meera.{objectId:N}@dewiride.test", null, [PersonRole.Finance]));

        using var administrator = Administrator();
        var created = Assert.Single(await ChangeRecordsApi.OfAsync(administrator, invited.Value));
        Assert.Equal(ChangeOperation.Created, created.Operation);
        Assert.Equal(ChangeActorKind.System, created.ActorKind);
        Assert.Equal(ActorIds.System, created.ActorId);
    }

    [Fact]
    public async Task RecordPositionAsync_PositionOfAPerson_RecordsItAndThePersonHoldsItToday()
    {
        using var administrator = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(administrator, OrganisationApi.UniqueName("Finance"));
        var manager = await PeopleApi.RegisterAsync(administrator, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var person = await PeopleApi.RegisterAsync(administrator, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var effectiveFrom = OrganisationApi.Today.AddDays(-10);

        var recorded = await RecordAsync(new PersonPosition(person.Id, effectiveFrom, department.Id, null, manager.Id));

        Assert.True(recorded.IsSuccess);
        var position = Assert.Single((await OrganisationApi.ReadPageAsync<PositionResponse>(administrator, OrganisationApi.Positions(person.Id))).Items);
        Assert.Equal(new PositionResponse(recorded.Value, effectiveFrom, department.Id, department.Name, null, null, manager.Id, manager.DisplayName), position);
        var read = await GetAsync(person.Id);
        Assert.Equal((department.Id, manager.Id), (read.DepartmentId, read.ManagerId));
    }

    [Fact]
    public async Task RecordPositionAsync_DeactivatedManagerForAPositionThatHoldsToday_AnswersManagerInactiveAndChangesNothing()
    {
        using var administrator = Administrator();
        var manager = await PeopleApi.RegisterAsync(administrator, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        using (var deactivated = await administrator.PutAsJsonAsync(PeopleApi.Path($"/{manager.Id}/status"), new { status = "deactivated", version = manager.Version }, PeopleApi.Json, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        }

        var person = await PeopleApi.RegisterAsync(administrator, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        var refused = await RecordAsync(new PersonPosition(person.Id, OrganisationApi.Today.AddDays(-10), null, null, manager.Id));

        Assert.Equal("position.manager-inactive", refused.Error?.Code);
        Assert.Equal(ErrorKind.Failure, refused.Error?.Kind);
        Assert.Empty((await OrganisationApi.ReadPageAsync<PositionResponse>(administrator, OrganisationApi.Positions(person.Id))).Items);
    }

    [Fact]
    public async Task RecordPositionAsync_PersonNoRecordHas_AnswersUserNotFound()
    {
        var refused = await RecordAsync(new PersonPosition(Guid.CreateVersion7(), OrganisationApi.Today, null, null, null));

        Assert.Equal("user.not-found", refused.Error?.Code);
        Assert.Equal(ErrorKind.NotFound, refused.Error?.Kind);
    }

    private async Task<Result<Guid>> InviteAsync(PersonInvitation invitation)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IPeopleCommands>().InvitePersonAsync(invitation, TestContext.Current.CancellationToken);
    }

    private async Task<Result<Guid>> RecordAsync(PersonPosition position)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IPeopleCommands>().RecordPositionAsync(position, TestContext.Current.CancellationToken);
    }

    private async Task<PersonResponse> GetAsync(Guid id)
    {
        using var administrator = Administrator();
        using var response = await administrator.GetAsync(PeopleApi.Path($"/{id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await PeopleApi.ReadPersonAsync(response);
    }

    private async Task<int> RecordsOfAsync(Guid objectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UsersDbContext>().Users.CountAsync(u => u.EntraObjectId == objectId, TestContext.Current.CancellationToken);
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
