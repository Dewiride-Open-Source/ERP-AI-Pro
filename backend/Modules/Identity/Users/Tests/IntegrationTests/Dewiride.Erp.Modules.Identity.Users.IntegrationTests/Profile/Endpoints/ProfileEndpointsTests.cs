using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Authentication.Graph;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.Profile.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;
using Dewiride.Erp.Testing.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Profile.Endpoints;

// Each test signs in a person of its own through the real session cookie, whose sign-in creates their record, so their photo,
// their theme and the photo the API keeps for them reach no other test.
public sealed class ProfileEndpointsTests(ProfileEndpointsTests.Fixture fixture) : IClassFixture<ProfileEndpointsTests.Fixture>
{
    private const string ProfileRoute = "/api/identity/users/me";

    private const string PhotoRoute = "/api/identity/users/me/photo";

    private const string ThemeRoute = "/api/identity/users/me/theme";

    private static readonly byte[] Png = Convert.FromHexString("89504E470D0A1A0A0000000D49484452");

    private static readonly byte[] Jpeg = Convert.FromHexString("FFD8FFE000104A464946");

    private static readonly string[] Finance = ["finance"];

    [Fact]
    public async Task Get_EmployeeSignedIn_AnswersTheirOwnRecordWithTodaysPositionAndTheRolesTheyHold()
    {
        var employee = NewPerson("Meera Nair", [AppRoles.User]);
        using var client = await InvitationApi.SignInAsync(fixture.Factory, employee);
        var record = await ProfileOfAsync(client);
        using var administrator = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);
        var department = await OrganisationApi.CreateDepartmentAsync(administrator, OrganisationApi.UniqueName("Finance"));
        var designation = await OrganisationApi.CreateDesignationAsync(administrator, OrganisationApi.UniqueName("Accountant"));
        var manager = await PeopleApi.RegisterAsync(administrator, new { displayName = "Arjun Rao", workEmail = PeopleApi.UniqueWorkEmail() });
        var laterDepartment = await OrganisationApi.CreateDepartmentAsync(administrator, OrganisationApi.UniqueName("Treasury"));
        var laterDesignation = await OrganisationApi.CreateDesignationAsync(administrator, OrganisationApi.UniqueName("Treasurer"));
        var laterManager = await PeopleApi.RegisterAsync(administrator, new { displayName = "Kavya Menon", workEmail = PeopleApi.UniqueWorkEmail() });
        await OrganisationApi.RecordPositionAsync(administrator, record.Id, OrganisationApi.Today, department.Id, designation.Id, manager.Id);
        await OrganisationApi.RecordPositionAsync(administrator, record.Id, OrganisationApi.Today.AddDays(30), laterDepartment.Id, laterDesignation.Id, laterManager.Id);
        await AssignFinanceAsync(administrator, record.Id);

        var profile = await ProfileOfAsync(client);

        Assert.Equal(record.Id, profile.Id);
        Assert.Equal("Meera Nair", profile.DisplayName);
        Assert.Equal(employee.UserName.ToLowerInvariant(), profile.WorkEmail);
        Assert.Null(profile.EmployeeCode);
        Assert.Null(profile.PhoneNumber);
        Assert.Null(profile.DateOfJoining);
        Assert.Equal(department.Name, profile.DepartmentName);
        Assert.Equal(designation.Name, profile.DesignationName);
        Assert.Equal("Arjun Rao", profile.ManagerDisplayName);
        Assert.Equal([ErpRole.Employee, ErpRole.Finance], profile.Roles);
        Assert.Equal(ThemePreference.System, profile.Theme);
        Assert.Null(profile.ThemeChoiceId);
    }

    [Fact]
    public async Task Get_AdministratorSignedIn_AnswersEmployeeAndAdministratorInTheOrderTheErpListsRoles()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Vikram Iyer", [AppRoles.Administrator, AppRoles.User]));

        var profile = await ProfileOfAsync(client);

        Assert.Equal([ErpRole.Employee, ErpRole.Administrator], profile.Roles);
        Assert.Null(profile.DepartmentName);
        Assert.Null(profile.DesignationName);
        Assert.Null(profile.ManagerDisplayName);
    }

    [Fact]
    public async Task Get_SignedInPerson_WritesTheRolesAndTheThemeAsCamelCaseNames()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));

        using var response = await client.GetAsync(new Uri(ProfileRoute, UriKind.Relative), TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            ["id", "displayName", "workEmail", "employeeCode", "phoneNumber", "dateOfJoining", "departmentName", "designationName", "managerDisplayName", "roles", "theme", "themeChoiceId"],
            body.RootElement.EnumerateObject().Select(member => member.Name));
        Assert.Equal(["employee"], body.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.Equal("system", body.RootElement.GetProperty("theme").GetString());
    }

    [Fact]
    public async Task Get_CallerWithoutARecord_Answers404UserNotFound()
    {
        await using var unknown = WithCallerWithoutARecord();
        using var client = unknown.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(new Uri(ProfileRoute, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("user.not-found", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task GetPhoto_PersonWithAPhotoOfTheSizeThePagesShow_AnswersItForTheBrowserToKeepAnHourForThisSignInOnly()
    {
        var person = NewPerson("Meera Nair", [AppRoles.User]);
        InvitationApi.DirectoryOf(fixture.Factory).Add(TestDirectoryPerson.Of(person) with { Photo = new TestDirectoryPhoto(Png, "image/png") });
        using var client = await InvitationApi.SignInAsync(fixture.Factory, person);

        using var response = await client.GetAsync(new Uri(PhotoRoute, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.Private);
        Assert.Equal(TimeSpan.FromHours(1), cacheControl.MaxAge);
        Assert.False(cacheControl.NoStore);
        Assert.Contains("Cookie", response.Headers.Vary);
        Assert.Contains("Authorization", response.Headers.Vary);
        var asked = Assert.Single(PhotoRequestsOf(person));
        Assert.Equal(TestDirectory.SizedPhotoPath, asked.Address.AbsolutePath);
        Assert.Contains(MicrosoftGraph.ReadOwnProfileScope, fixture.TokenEndpoint.ScopeOf(asked.AccessToken!)!.Split(' '));
    }

    [Fact]
    public async Task GetPhoto_PhotoKeptAtASizeOfItsOwn_AnswersTheLargestPhoto()
    {
        var person = NewPerson("Meera Nair", [AppRoles.User]);
        InvitationApi.DirectoryOf(fixture.Factory).Add(TestDirectoryPerson.Of(person) with { Photo = new TestDirectoryPhoto(Jpeg, "image/jpeg", Sized: false) });
        using var client = await InvitationApi.SignInAsync(fixture.Factory, person);

        using var response = await client.GetAsync(new Uri(PhotoRoute, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Jpeg, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal([TestDirectory.SizedPhotoPath, TestDirectory.PhotoPath], PhotoRequestsOf(person).Select(request => request.Address.AbsolutePath));
    }

    [Fact]
    public async Task GetPhoto_PersonWithoutAPhoto_Answers204ThatTheBrowserDoesNotKeep()
    {
        var person = NewPerson("Meera Nair", [AppRoles.User]);
        InvitationApi.DirectoryOf(fixture.Factory).Add(TestDirectoryPerson.Of(person));
        using var client = await InvitationApi.SignInAsync(fixture.Factory, person);

        using var response = await client.GetAsync(new Uri(PhotoRoute, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task GetPhoto_AskedAgainWhileTheAnswerIsKept_AsksTheDirectoryNoMore()
    {
        var withPhoto = NewPerson("Meera Nair", [AppRoles.User]);
        var withoutPhoto = NewPerson("Arjun Rao", [AppRoles.User]);
        InvitationApi.DirectoryOf(fixture.Factory).Add(TestDirectoryPerson.Of(withPhoto) with { Photo = new TestDirectoryPhoto(Png, "image/png") });
        using var first = await InvitationApi.SignInAsync(fixture.Factory, withPhoto);
        using var second = await InvitationApi.SignInAsync(fixture.Factory, withoutPhoto);

        foreach (var client in new[] { first, first, second, second })
        {
            using var response = await client.GetAsync(new Uri(PhotoRoute, UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.True(response.IsSuccessStatusCode);
        }

        Assert.Single(PhotoRequestsOf(withPhoto));
        Assert.Equal(2, PhotoRequestsOf(withoutPhoto).Count);
    }

    [Fact]
    public async Task GetPhoto_DirectoryThatRefusesThePerson_Answers403AccessDeniedAndAsksAgainNextTime()
    {
        await using var factory = new ErpApiFactory().WithTestEndpoints(TestSignIn.Map);
        var directory = InvitationApi.DirectoryOf(factory);
        directory.RefuseEveryRequest();
        using var client = await InvitationApi.SignInAsync(factory, NewPerson("Meera Nair", [AppRoles.User], factory));

        using var first = await client.GetAsync(new Uri(PhotoRoute, UriKind.Relative), TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(new Uri(PhotoRoute, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, first.StatusCode);
        Assert.Equal("directory.access-denied", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(first)));
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
        Assert.Equal(2, directory.Requests.Count);
    }

    [Fact]
    public async Task GetPhoto_PersonWithoutASessionOfTheirOwn_Answers403AccessDenied()
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(new Uri(PhotoRoute, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("directory.access-denied", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task ChooseTheme_SignedInPerson_AnswersTheChoiceTheirProfileThenHolds()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));

        var chosen = await ChooseThemeAsync(client, "dark");

        Assert.Equal(ThemePreference.Dark, chosen.Theme);
        Assert.NotNull(chosen.ChoiceId);
        var profile = await ProfileOfAsync(client);
        Assert.Equal(ThemePreference.Dark, profile.Theme);
        Assert.Equal(chosen.ChoiceId, profile.ThemeChoiceId);
    }

    [Fact]
    public async Task ChooseTheme_SignedInPerson_WritesTheThemeAndItsChoiceAsCamelCaseNames()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));

        using var response = await PutThemeAsync(client, new { theme = "light" });
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["theme", "choiceId"], body.RootElement.EnumerateObject().Select(member => member.Name));
        Assert.Equal("light", body.RootElement.GetProperty("theme").GetString());
    }

    [Fact]
    public async Task ChooseTheme_LaterChoice_ReplacesTheEarlierOneWithAChoiceOfItsOwn()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));
        var dark = await ChooseThemeAsync(client, "dark");

        var light = await ChooseThemeAsync(client, "light");

        Assert.Equal(ThemePreference.Light, light.Theme);
        Assert.NotEqual(dark.ChoiceId, light.ChoiceId);
        var profile = await ProfileOfAsync(client);
        Assert.Equal(ThemePreference.Light, profile.Theme);
        Assert.Equal(light.ChoiceId, profile.ThemeChoiceId);
    }

    [Fact]
    public async Task ChooseTheme_TheThemeTheAccountHolds_AnswersTheSameChoice()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));
        var first = await ChooseThemeAsync(client, "dark");

        var again = await ChooseThemeAsync(client, "dark");

        Assert.Equal(first, again);
    }

    [Fact]
    public async Task ChooseTheme_ThemeChosenAgainAfterAnother_AnswersANewChoice()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));
        var dark = await ChooseThemeAsync(client, "dark");
        await ChooseThemeAsync(client, "light");

        var darkAgain = await ChooseThemeAsync(client, "dark");

        Assert.Equal(ThemePreference.Dark, darkAgain.Theme);
        Assert.NotEqual(dark.ChoiceId, darkAgain.ChoiceId);
    }

    [Fact]
    public async Task ChooseTheme_SystemBeforeAnyOtherTheme_KeepsTheThemeOfTheirDeviceWithoutAChoice()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));

        var chosen = await ChooseThemeAsync(client, "system");

        Assert.Equal(ThemePreference.System, chosen.Theme);
        Assert.Null(chosen.ChoiceId);
        Assert.Null((await ProfileOfAsync(client)).ThemeChoiceId);
    }

    [Fact]
    public async Task ChooseTheme_WhileAnAdministratorEditsTheirRecord_LeavesTheVersionTheEditHolds()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));
        var record = await ProfileOfAsync(client);
        using var administrator = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);
        using var read = await administrator.GetAsync(PeopleApi.Path($"/{record.Id}"), TestContext.Current.CancellationToken);
        var opened = await PeopleApi.ReadPersonAsync(read);

        await ChooseThemeAsync(client, "dark");
        await ChooseThemeAsync(client, "light");
        using var saved = await administrator.PutAsJsonAsync(
            PeopleApi.Path($"/{record.Id}"),
            new { phoneNumber = "+91 98765 00005", version = opened.Version },
            PeopleApi.Json,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    [Fact]
    public async Task ChooseTheme_SignedInPerson_RecordsEachThemeUnderThemWithoutItsChoiceOrAChangeOfTheirRecord()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));
        var record = await ProfileOfAsync(client);
        using var administrator = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);
        var before = await ChangeRecordsApi.OfAsync(administrator, record.Id);

        await ChooseThemeAsync(client, "dark");
        await ChooseThemeAsync(client, "dark");
        await ChooseThemeAsync(client, "light");

        var themes = (await ChangeRecordsApi.OfAsync(administrator, record.Id)).Skip(before.Count).ToList();
        Assert.NotEmpty(before);
        Assert.All(before, change => Assert.Equal(ChangeRecordsApi.PeopleKey, change.EntityType));
        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated], themes.Select(change => change.Operation));
        Assert.All(themes, change =>
        {
            Assert.Equal(ChangeRecordsApi.ThemesKey, change.EntityType);
            Assert.Equal(record.Id, change.EntityId);
            Assert.Equal(ChangeRecordsApi.PeopleKey, change.SubjectType);
        });
        Assert.Equal(new RecordedValue("theme", ChangeValueKind.Enum, null, "dark", null, null, null), Assert.Single(themes[0].Changes));
        Assert.Equal(new RecordedValue("theme", ChangeValueKind.Enum, "dark", "light", null, null, null), Assert.Single(themes[1].Changes));
    }

    [Theory]
    [InlineData("sepia")]
    [InlineData("")]
    public async Task ChooseTheme_ThemeTheWebAppHasNot_Answers400RequestMalformed(string theme)
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));

        using var response = await PutThemeAsync(client, new { theme });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.malformed", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task ChooseTheme_ThemeAsANumber_Answers400RequestMalformed()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));

        using var response = await PutThemeAsync(client, new { theme = 3 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.malformed", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task ChooseTheme_WithoutATheme_Answers400KeyedByTheTheme()
    {
        using var client = await InvitationApi.SignInAsync(fixture.Factory, NewPerson("Meera Nair", [AppRoles.User]));

        using var response = await PutThemeAsync(client, new { });

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Equal("theme", Assert.Single(problem.Errors.Keys));
    }

    [Fact]
    public async Task ChooseTheme_CallerWithoutARecord_Answers404UserNotFound()
    {
        await using var unknown = WithCallerWithoutARecord();
        using var client = unknown.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await PutThemeAsync(client, new { theme = "dark" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("user.not-found", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Theory]
    [InlineData("GET", ProfileRoute)]
    [InlineData("GET", PhotoRoute)]
    [InlineData("PUT", ThemeRoute)]
    public void Route_OfTheCallersOwnRecord_RequiresNoPermission(string method, string route)
    {
        Assert.Null(RoutePermissions.Of(fixture.Factory.Services, method, route));
    }

    [Theory]
    [InlineData("GET", ProfileRoute)]
    [InlineData("GET", PhotoRoute)]
    [InlineData("PUT", ThemeRoute)]
    public async Task Route_Anonymous_Answers401(string method, string route)
    {
        using var client = fixture.Factory.CreateClient();
        using var request = Request(method, route);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", ProfileRoute)]
    [InlineData("GET", PhotoRoute)]
    [InlineData("PUT", ThemeRoute)]
    public async Task Route_ModuleDisabled_AnswersFeatureDisabled(string method, string route)
    {
        await using var disabled = new ErpApiFactory().WithFeature(UsersModule.FeatureFlag, enabled: false);
        using var client = disabled.CreateClient().AsUser(TestUsers.Accountant);
        using var request = Request(method, route);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("feature.disabled", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    private static HttpRequestMessage Request(string method, string route) =>
        new(new HttpMethod(method), route) { Content = method == "PUT" ? JsonContent.Create(new { theme = "dark" }, options: PeopleApi.Json) : null };

    private static async Task<ProfileResponse> ProfileOfAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri(ProfileRoute, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await OrganisationApi.ReadAsync<ProfileResponse>(response);
    }

    private static Task<HttpResponseMessage> PutThemeAsync(HttpClient client, object body) =>
        client.PutAsJsonAsync(new Uri(ThemeRoute, UriKind.Relative), body, PeopleApi.Json, TestContext.Current.CancellationToken);

    private static async Task<ThemeChoiceResponse> ChooseThemeAsync(HttpClient client, string theme)
    {
        using var response = await PutThemeAsync(client, new { theme });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await OrganisationApi.ReadAsync<ThemeChoiceResponse>(response);
    }

    private static async Task AssignFinanceAsync(HttpClient administrator, Guid id)
    {
        using var read = await administrator.GetAsync(PeopleApi.Path($"/{id}"), TestContext.Current.CancellationToken);
        var person = await PeopleApi.ReadPersonAsync(read);
        using var assigned = await administrator.PutAsJsonAsync(PeopleApi.Path($"/{id}/roles"), new { roles = Finance, version = person.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
    }

    private TestUser NewPerson(string name, IReadOnlyList<string> appRoles, ErpApiFactory? factory = null)
    {
        var objectId = Guid.CreateVersion7();
        var person = new TestUser(objectId, name, $"{name.Replace(' ', '.')}.{objectId:N}@Dewiride.test", appRoles);
        (factory ?? fixture.Factory).Services.GetRequiredService<TestTokenEndpoint>().Admit(person);

        return person;
    }

    private List<TestDirectoryRequest> PhotoRequestsOf(TestUser person) =>
        [.. InvitationApi.DirectoryOf(fixture.Factory).Requests.Where(request =>
            request.Address.AbsolutePath is TestDirectory.SizedPhotoPath or TestDirectory.PhotoPath
            && request.AccessToken is { } token
            && fixture.TokenEndpoint.SubjectOf(token) == person.ObjectId)];

    private WebApplicationFactory<Program> WithCallerWithoutARecord() =>
        fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddScoped<IActorContext, CallerWithoutARecord>()));

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(TestSignIn.Map);

        public TestTokenEndpoint TokenEndpoint => Factory.Services.GetRequiredService<TestTokenEndpoint>();

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }

    private sealed class CallerWithoutARecord : IActorContext
    {
        public Guid ActorId { get; } = Guid.CreateVersion7();

        public bool IsAuthenticated => true;
    }
}
