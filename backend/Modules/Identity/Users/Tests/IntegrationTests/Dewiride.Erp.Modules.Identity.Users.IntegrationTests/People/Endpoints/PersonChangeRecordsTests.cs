using System.Net;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;

public sealed class PersonChangeRecordsTests(PersonChangeRecordsTests.Fixture fixture) : IClassFixture<PersonChangeRecordsTests.Fixture>
{
    private static readonly string[] HrAndFinance = ["hr", "finance"];

    [Fact]
    public async Task Register_Person_RecordsTheCreationWithEveryValueOfTheRecord()
    {
        using var client = Administrator();
        var workEmail = PeopleApi.UniqueWorkEmail();
        var employeeCode = PeopleApi.UniqueEmployeeCode();
        PersonResponse person;
        string correlationId;
        using (var response = await PeopleApi.PostAsync(client, PeopleApi.Person(workEmail, employeeCode)))
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            person = await PeopleApi.ReadPersonAsync(response);
            correlationId = ChangeRecordsApi.CorrelationIdOf(response);
        }

        var created = Assert.Single(await ChangeRecordsApi.OfAsync(client, person.Id));

        Assert.Equal(ChangeOperation.Created, created.Operation);
        Assert.Equal(ChangeRecordsApi.PeopleKey, created.EntityType);
        Assert.Equal(person.Id, created.EntityId);
        Assert.Equal(ChangeRecordsApi.PeopleKey, created.SubjectType);
        Assert.Equal(person.Id, created.SubjectId);
        Assert.Equal(person.DisplayName, created.SubjectName);
        Assert.Equal(ChangeActorKind.Person, created.ActorKind);
        Assert.Equal(TestUsers.Administrator.ObjectId, created.ActorId);
        Assert.Equal(correlationId, created.CorrelationId);
        Assert.Equal(
            [
                new RecordedValue("dateOfJoining", ChangeValueKind.Date, null, "2026-04-01", null, null, null),
                new RecordedValue("displayName", ChangeValueKind.Text, null, "Meera Nair", null, null, null),
                new RecordedValue("employeeCode", ChangeValueKind.Text, null, employeeCode, null, null, null),
                new RecordedValue("entraObjectId", ChangeValueKind.Id, null, null, null, null, null),
                new RecordedValue("phoneNumber", ChangeValueKind.Text, null, "+919876543210", null, null, null),
                new RecordedValue("roles", ChangeValueKind.List, null, "[]", null, null, null),
                new RecordedValue("status", ChangeValueKind.Enum, null, "active", null, null, null),
                new RecordedValue("workEmail", ChangeValueKind.Text, null, workEmail, null, null, null),
            ],
            created.ValuesByProperty);
    }

    [Fact]
    public async Task Update_Details_RecordsOnlyTheValuesThatChanged()
    {
        using var client = Administrator();
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var employeeCode = PeopleApi.UniqueEmployeeCode();
        using (var response = await client.PutAsJsonAsync(
            PeopleApi.Path($"/{registered.Id}"),
            new { employeeCode, phoneNumber = "098765 43210", dateOfJoining = "2025-12-01", version = registered.Version },
            PeopleApi.Json,
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var changes = await ChangeRecordsApi.OfAsync(client, registered.Id);

        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated], changes.Select(change => change.Operation));
        Assert.Equal(
            [
                new RecordedValue("dateOfJoining", ChangeValueKind.Date, "2026-04-01", "2025-12-01", null, null, null),
                new RecordedValue("employeeCode", ChangeValueKind.Text, null, employeeCode, null, null, null),
            ],
            changes[1].ValuesByProperty);
    }

    [Fact]
    public async Task AssignRoles_RecordsTheRolesBeforeAndAfter()
    {
        using var client = Administrator();
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        using (var response = await client.PutAsJsonAsync(
            PeopleApi.Path($"/{registered.Id}/roles"),
            new { roles = HrAndFinance, version = registered.Version },
            PeopleApi.Json,
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var changes = await ChangeRecordsApi.OfAsync(client, registered.Id);

        Assert.Equal(
            new RecordedValue("roles", ChangeValueKind.List, "[]", "[\"finance\",\"hr\"]", null, null, null),
            Assert.Single(changes[^1].Changes));
    }

    [Fact]
    public async Task ChangeStatus_Deactivate_RecordsTheStatusBeforeAndAfter()
    {
        using var client = Administrator();
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        using (var response = await client.PutAsJsonAsync(
            PeopleApi.Path($"/{registered.Id}/status"),
            new { status = "deactivated", version = registered.Version },
            PeopleApi.Json,
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var changes = await ChangeRecordsApi.OfAsync(client, registered.Id);

        Assert.Equal(new RecordedValue("status", ChangeValueKind.Enum, "active", "deactivated", null, null, null), Assert.Single(changes[^1].Changes));
    }

    [Fact]
    public async Task Delete_Person_RecordsTheDeletionWithoutValuesAndStillNamesThePerson()
    {
        using var client = Administrator();
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        using (var response = await client.DeleteAsync(PeopleApi.Path($"/{registered.Id}"), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var changes = await ChangeRecordsApi.OfAsync(client, registered.Id);

        Assert.Equal([ChangeOperation.Created, ChangeOperation.Deleted], changes.Select(change => change.Operation));
        Assert.Empty(changes[1].Changes);
        Assert.All(changes, change => Assert.Equal(registered.DisplayName, change.SubjectName));
    }

    [Fact]
    public async Task SignIn_FirstOfAPersonWithoutARecord_RecordsTheCreationOfTheirRecordAsMadeByThemself()
    {
        var person = NewPerson();
        using var session = TestSignIn.CreateClient(fixture.Factory);

        using var signIn = await TestSignIn.SignInAsync(session, person);

        var created = Assert.Single(await ChangeRecordsApi.OfAsync(Administrator(), await RecordIdOfAsync(person)));
        Assert.Equal(ChangeOperation.Created, created.Operation);
        Assert.Equal(ChangeActorKind.Person, created.ActorKind);
        Assert.Equal(person.ObjectId, created.ActorId);
        Assert.Equal(person.Name, created.ActorName);
        Assert.Equal(new RecordedValue("entraObjectId", ChangeValueKind.Id, null, person.ObjectId.ToString("D"), null, null, null), Value(created, "entraObjectId"));
        Assert.Equal(new RecordedValue("workEmail", ChangeValueKind.Text, null, person.UserName, null, null, null), Value(created, "workEmail"));
        Assert.DoesNotContain(created.Changes, value => value.Property == "lastSignedInAt");
    }

    [Fact]
    public async Task SignIn_AgainChangingOnlyTheSignInTime_RecordsNothing()
    {
        var person = NewPerson();
        using var first = TestSignIn.CreateClient(fixture.Factory);
        using var firstSignIn = await TestSignIn.SignInAsync(first, person);
        var signedInFirst = await LastSignedInAtOfAsync(person);
        using var second = TestSignIn.CreateClient(fixture.Factory);

        using var secondSignIn = await TestSignIn.SignInAsync(second, person);

        Assert.True(await LastSignedInAtOfAsync(person) > signedInFirst);
        Assert.Equal(ChangeOperation.Created, Assert.Single(await ChangeRecordsApi.OfAsync(Administrator(), await RecordIdOfAsync(person))).Operation);
    }

    [Fact]
    public async Task SignIn_OfAPersonRegisteredByTheirWorkEmail_RecordsTheLinkOfTheirAccountAsMadeByThemself()
    {
        var person = NewPerson();
        var registered = await PeopleApi.RegisterAsync(Administrator(), new { displayName = person.Name, workEmail = person.UserName });
        using var session = TestSignIn.CreateClient(fixture.Factory);

        using var signIn = await TestSignIn.SignInAsync(session, person);

        var changes = await ChangeRecordsApi.OfAsync(Administrator(), registered.Id);
        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated], changes.Select(change => change.Operation));
        Assert.Equal(TestUsers.Administrator.ObjectId, changes[0].ActorId);
        Assert.Equal(person.ObjectId, changes[1].ActorId);
        Assert.Equal(person.Name, changes[1].ActorName);
        Assert.Equal(new RecordedValue("entraObjectId", ChangeValueKind.Id, null, person.ObjectId.ToString("D"), null, null, null), Assert.Single(changes[1].Changes));
    }

    private static RecordedValue Value(RecordedChange change, string property) => Assert.Single(change.Changes, value => value.Property == property);

    private HttpClient Administrator() => fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);

    private TestUser NewPerson()
    {
        var objectId = Guid.CreateVersion7();
        var person = new TestUser(objectId, "Ravi Kumar", $"ravi.kumar.{objectId:N}@example.com", [AppRoles.User]);
        fixture.Factory.Services.GetRequiredService<TestTokenEndpoint>().Admit(person);

        return person;
    }

    private async Task<Guid> RecordIdOfAsync(TestUser person)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<UsersDbContext>().Users
            .Where(u => u.EntraObjectId == person.ObjectId)
            .Select(u => u.Id)
            .SingleAsync(TestContext.Current.CancellationToken)).Value;
    }

    private async Task<DateTimeOffset?> LastSignedInAtOfAsync(TestUser person)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UsersDbContext>().Users
            .Where(u => u.EntraObjectId == person.ObjectId)
            .Select(u => u.LastSignedInAt)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(TestSignIn.Map);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
