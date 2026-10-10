using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication.Graph;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Graph;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;

public sealed class InvitationEndpointsTests(InvitationEndpointsTests.Fixture fixture) : IClassFixture<InvitationEndpointsTests.Fixture>
{
    [Fact]
    public async Task SearchDirectory_AsSignedInAdministrator_AnswersTheMatchingPeopleByDisplayNameReadOnTheirBehalf()
    {
        var word = InvitationApi.UniqueWord();
        var zara = InvitationApi.DirectoryPerson($"Zara {word} Iyer");
        var arjun = InvitationApi.DirectoryPerson($"Arjun {word}") with { Mail = "arjun.rao@dewiride.test" };
        var directory = InvitationApi.DirectoryOf(fixture.Factory);
        directory.Add(zara);
        directory.Add(arjun);
        directory.Add(InvitationApi.DirectoryPerson($"Kavya {InvitationApi.UniqueWord()}"));
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);

        var search = await InvitationApi.SearchAsync(client, $" {word.ToUpperInvariant()} ");

        Assert.False(search.HasMore);
        Assert.Equal([arjun.ObjectId, zara.ObjectId], search.People.Select(person => person.EntraObjectId));
        var first = search.People[0];
        Assert.Equal(arjun.DisplayName, first.DisplayName);
        Assert.Equal(arjun.UserPrincipalName, first.UserPrincipalName);
        Assert.Equal("arjun.rao@dewiride.test", first.Mail);
        Assert.Null(first.PersonId);
        Assert.Null(search.People[1].Mail);
        var request = directory.Requests.Last(r => r.Address.Query.Contains(word.ToUpperInvariant(), StringComparison.Ordinal));
        Assert.Equal(MicrosoftGraph.EventualConsistency, request.ConsistencyLevel);
        Assert.Contains(MicrosoftGraph.ReadBasicProfilesScope, fixture.TokenEndpoint.ScopeOf(request.AccessToken!)!.Split(' '));
    }

    [Fact]
    public async Task SearchDirectory_PeopleTheErpHolds_NamesTheRecordsTheirSignInWouldBeAdmittedWith()
    {
        var word = InvitationApi.UniqueWord();
        var invited = InvitationApi.DirectoryPerson($"Anil {word}");
        var registered = InvitationApi.DirectoryPerson($"Bina {word}");
        var unknown = InvitationApi.DirectoryPerson($"Chitra {word}");
        var reassigned = InvitationApi.DirectoryPerson($"Dev {word}");
        var directory = InvitationApi.DirectoryOf(fixture.Factory);
        foreach (var person in new[] { invited, registered, unknown, reassigned })
        {
            directory.Add(person);
        }

        var invitedRecord = await AddRecordAsync(User.Invite(invited.ObjectId, invited.DisplayName, invited.UserPrincipalName, null, null, null, null).Value);
        var registeredRecord = await AddRecordAsync(User.Register(registered.DisplayName, registered.UserPrincipalName.ToUpperInvariant(), null, null, null, null).Value);
        await AddRecordAsync(User.Invite(Guid.CreateVersion7(), "Dev Earlier", reassigned.UserPrincipalName, null, null, null, null).Value);
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);

        var search = await InvitationApi.SearchAsync(client, word);

        Assert.Equal([invited.ObjectId, registered.ObjectId, unknown.ObjectId, reassigned.ObjectId], search.People.Select(person => person.EntraObjectId));
        Assert.Equal([invitedRecord.Value, registeredRecord.Value, null, null], search.People.Select(person => person.PersonId));
    }

    [Fact]
    public async Task SearchDirectory_GuestRegisteredByTheMailTheySignInWith_NamesTheirRecord()
    {
        var word = InvitationApi.UniqueWord();
        var guest = Guest(word);
        InvitationApi.DirectoryOf(fixture.Factory).Add(guest);
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(guest.Mail!));

        var search = await InvitationApi.SearchAsync(client, word);

        var found = Assert.Single(search.People);
        Assert.Equal(guest.Mail, found.SignInName);
        Assert.Equal(guest.UserPrincipalName, found.UserPrincipalName);
        Assert.Equal(registered.Id, found.PersonId);
    }

    [Fact]
    public async Task Invite_GuestOfAnotherOrganisation_RecordsTheMailTheySignInWithAsTheWorkEmail()
    {
        var guest = Guest(InvitationApi.UniqueWord());
        InvitationApi.DirectoryOf(fixture.Factory).Add(guest);
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);

        var invited = await InvitationApi.InviteAsync(client, guest.ObjectId);

        Assert.Equal(guest.ObjectId, invited.EntraObjectId);
        Assert.Equal(guest.Mail!.ToLowerInvariant(), invited.WorkEmail);
    }

    [Fact]
    public async Task SearchDirectory_MorePeopleMatchThanOneAnswerHolds_AnswersTheFirst25AndSaysMoreMatched()
    {
        var word = InvitationApi.UniqueWord();
        var directory = InvitationApi.DirectoryOf(fixture.Factory);
        for (var index = 0; index < 26; index++)
        {
            directory.Add(InvitationApi.DirectoryPerson($"{word} {index:D2}"));
        }

        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);

        var search = await InvitationApi.SearchAsync(client, word);

        Assert.True(search.HasMore);
        Assert.Equal(25, search.People.Count);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("  b  ")]
    [InlineData("ravi & co")]
    [InlineData("ravi\u0001kumar")]
    public async Task SearchDirectory_TextTheDirectoryCannotSearch_Returns400KeyedBySearchWithoutAskingTheDirectory(string text)
    {
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);
        var asked = InvitationApi.DirectoryOf(fixture.Factory).Requests.Count;

        using var response = await client.GetAsync(InvitationApi.SearchPath(text), TestContext.Current.CancellationToken);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Equal("search", Assert.Single(problem.Errors.Keys));
        Assert.Equal(asked, InvitationApi.DirectoryOf(fixture.Factory).Requests.Count);
    }

    [Fact]
    public async Task SearchDirectory_AdministratorWithoutASessionOfTheirOwn_Returns403AccessDenied()
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.GetAsync(InvitationApi.SearchPath("meera"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("directory.access-denied", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task SearchDirectory_EntraWithoutTheConsentOfTheAdministrator_Returns403AccessDeniedWithoutAskingTheDirectory()
    {
        var administrator = InvitationApi.NewAdministrator(fixture.Factory);
        using var client = await InvitationApi.SignInAsync(fixture.Factory, administrator);
        fixture.TokenEndpoint.WithholdConsent(administrator);

        using var response = await client.GetAsync(InvitationApi.SearchPath("meera"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("directory.access-denied", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        Assert.DoesNotContain(InvitationApi.DirectoryOf(fixture.Factory).Requests, request => request.AccessToken is { } token && token.Contains(administrator.ObjectId.ToString("N"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task SearchDirectory_DirectoryThatRefusesTheAdministrator_Returns403AccessDenied()
    {
        await using var factory = new ErpApiFactory().WithTestEndpoints(TestSignIn.Map);
        InvitationApi.DirectoryOf(factory).RefuseEveryRequest();
        using var client = await InvitationApi.SignedInAdministratorAsync(factory);

        using var response = await client.GetAsync(InvitationApi.SearchPath("meera"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("directory.access-denied", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task Invite_PersonOfTheDirectory_Returns201WithARecordLinkedToTheirAccountWithTheNamesOfTheDirectory()
    {
        var person = InvitationApi.DirectoryPerson("Meera Nair") with { UserPrincipalName = PeopleApi.UniqueWorkEmail().ToUpperInvariant() };
        InvitationApi.DirectoryOf(fixture.Factory).Add(person);
        var employeeCode = PeopleApi.UniqueEmployeeCode();
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);

        using var response = await InvitationApi.PostAsync(client, InvitationApi.Invitation(person.ObjectId, employeeCode));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invited = await PeopleApi.ReadPersonAsync(response);
        Assert.Equal(PeopleApi.Path($"/{invited.Id}"), response.Headers.Location);
        Assert.Equal(person.ObjectId, invited.EntraObjectId);
        Assert.Equal("Meera Nair", invited.DisplayName);
        Assert.Equal(person.UserPrincipalName.ToLowerInvariant(), invited.WorkEmail);
        Assert.Equal(employeeCode, invited.EmployeeCode);
        Assert.Equal("+919876543210", invited.PhoneNumber);
        Assert.Equal("Accountant", invited.Designation);
        Assert.Equal(new DateOnly(2026, 4, 1), invited.DateOfJoining);
        Assert.Equal(PersonStatus.Active, invited.Status);
        Assert.Null(invited.LastSignedInAt);
    }

    [Fact]
    public async Task Invite_ThenTheFirstSignInOfThePerson_SignsThemInToTheInvitedRecord()
    {
        var objectId = Guid.CreateVersion7();
        var person = new TestUser(objectId, "Ravi Kumar", $"ravi.kumar.{objectId:N}@dewiride.test", []);
        fixture.TokenEndpoint.Admit(person);
        InvitationApi.DirectoryOf(fixture.Factory).Add(TestDirectoryPerson.Of(person));
        using var administrator = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);
        var invited = await InvitationApi.InviteAsync(administrator, objectId);
        using var client = TestSignIn.CreateClient(fixture.Factory);

        using var signIn = await TestSignIn.SignInAsync(client, person);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var records = await scope.ServiceProvider.GetRequiredService<UsersDbContext>().Users
            .AsNoTracking()
            .Where(u => u.EntraObjectId == objectId)
            .ToListAsync(TestContext.Current.CancellationToken);
        var record = Assert.Single(records);
        Assert.Equal(invited.Id, record.Id.Value);
        Assert.NotNull(record.LastSignedInAt);
    }

    [Fact]
    public async Task Invite_SameIdempotencyKeyTwice_InvitesOnceAndAsksTheDirectoryOnce()
    {
        var person = InvitationApi.DirectoryPerson($"Meera {InvitationApi.UniqueWord()}");
        var directory = InvitationApi.DirectoryOf(fixture.Factory);
        directory.Add(person);
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);
        var key = Guid.CreateVersion7().ToString();

        using var first = await InvitationApi.PostAsync(client, InvitationApi.Invitation(person.ObjectId), key);
        using var second = await InvitationApi.PostAsync(client, InvitationApi.Invitation(person.ObjectId), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal((await PeopleApi.ReadPersonAsync(first)).Id, (await PeopleApi.ReadPersonAsync(second)).Id);
        Assert.Single(directory.Requests, request => request.Address.AbsolutePath.EndsWith(person.ObjectId.ToString("D"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invite_ObjectIdTheDirectoryDoesNotHold_Returns404PersonNotFound()
    {
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);

        using var response = await InvitationApi.PostAsync(client, InvitationApi.Invitation(Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("directory.person-not-found", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task Invite_PersonWhoseAccountARecordHolds_Returns409EntraObjectIdTaken()
    {
        var person = InvitationApi.DirectoryPerson("Meera Nair");
        InvitationApi.DirectoryOf(fixture.Factory).Add(person);
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);
        await InvitationApi.InviteAsync(client, person.ObjectId);

        using var response = await InvitationApi.PostAsync(client, InvitationApi.Invitation(person.ObjectId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.entra-object-id-taken", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task Invite_PersonRegisteredByTheirSignInName_Returns409WorkEmailTaken()
    {
        var person = InvitationApi.DirectoryPerson("Meera Nair");
        InvitationApi.DirectoryOf(fixture.Factory).Add(person);
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);
        await PeopleApi.RegisterAsync(client, PeopleApi.Person(person.UserPrincipalName));

        using var response = await InvitationApi.PostAsync(client, InvitationApi.Invitation(person.ObjectId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.work-email-taken", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task Invite_SignInNameThatALinkedRecordOfAnotherAccountKept_Returns201()
    {
        var person = InvitationApi.DirectoryPerson("Meera Nair");
        InvitationApi.DirectoryOf(fixture.Factory).Add(person);
        await AddRecordAsync(User.Invite(Guid.CreateVersion7(), "Meera Earlier", person.UserPrincipalName, null, null, null, null).Value);
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);

        using var response = await InvitationApi.PostAsync(client, InvitationApi.Invitation(person.ObjectId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(person.ObjectId, (await PeopleApi.ReadPersonAsync(response)).EntraObjectId);
    }

    [Fact]
    public async Task Invite_EmployeeCodeOfAnotherRecord_Returns409EmployeeCodeTaken()
    {
        var person = InvitationApi.DirectoryPerson("Meera Nair");
        InvitationApi.DirectoryOf(fixture.Factory).Add(person);
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);
        var employeeCode = PeopleApi.UniqueEmployeeCode();
        await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail(), employeeCode));

        using var response = await InvitationApi.PostAsync(client, InvitationApi.Invitation(person.ObjectId, employeeCode));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.employee-code-taken", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Invite_WithoutAnObjectId_Returns400KeyedByEntraObjectId(string? objectId)
    {
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);

        using var response = await InvitationApi.PostAsync(client, InvitationApi.Invitation(objectId is null ? null : Guid.Parse(objectId)));

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Equal("entraObjectId", Assert.Single(problem.Errors.Keys));
    }

    [Fact]
    public async Task Invite_WithoutAnIdempotencyKey_Returns400WithoutAskingTheDirectory()
    {
        var person = InvitationApi.DirectoryPerson("Meera Nair");
        var directory = InvitationApi.DirectoryOf(fixture.Factory);
        directory.Add(person);
        using var client = await InvitationApi.SignedInAdministratorAsync(fixture.Factory);

        using var response = await client.PostAsJsonAsync(InvitationApi.InvitationsPath, InvitationApi.Invitation(person.ObjectId), PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("idempotency.key-missing", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        Assert.DoesNotContain(directory.Requests, request => request.Address.AbsolutePath.EndsWith(person.ObjectId.ToString("D"), StringComparison.Ordinal));
    }

    private static TestDirectoryPerson Guest(string word) =>
        new(Guid.CreateVersion7(), $"John {word}", $"{word}_contoso.test#EXT#@dewiride.onmicrosoft.com", $"John.{word}@Contoso.test");

    private async Task<UserId> AddRecordAsync(User user)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user.Id;
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(TestSignIn.Map);

        public TestTokenEndpoint TokenEndpoint => Factory.Services.GetRequiredService<TestTokenEndpoint>();

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
