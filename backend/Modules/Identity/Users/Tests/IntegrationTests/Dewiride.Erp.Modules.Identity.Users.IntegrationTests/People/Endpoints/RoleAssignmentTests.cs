using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.TestHost;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;

public sealed class RoleAssignmentTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static readonly string[] Hr = ["hr"];

    private static readonly string[] Finance = ["finance"];

    [Fact]
    public async Task PutRoles_AsAdministrator_ReplacesTheRolesAndAnswersTheRecordWithANewVersion()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await PutRolesAsync(client, registered.Id, ["manager", "finance", "manager"], registered.Version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assigned = await PeopleApi.ReadPersonAsync(response);
        Assert.Equal([PersonRole.Finance, PersonRole.Manager], assigned.Roles);
        Assert.NotEqual(registered.Version, assigned.Version);
        using var read = await client.GetAsync(PeopleApi.Path($"/{registered.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal([PersonRole.Finance, PersonRole.Manager], (await PeopleApi.ReadPersonAsync(read)).Roles);
    }

    [Fact]
    public async Task PutRoles_FromAVersionTheRecordNoLongerHas_Answers409ConcurrencyConflict()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        using var first = await PutRolesAsync(client, registered.Id, ["hr"], registered.Version);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var response = await PutRolesAsync(client, registered.Id, ["finance"], registered.Version);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("concurrency.conflict", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task PutRoles_TheRolesTheRecordAlreadyHolds_KeepsItsVersion()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await PutRolesAsync(client, registered.Id, [], registered.Version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var unchanged = await PeopleApi.ReadPersonAsync(response);
        Assert.Empty(unchanged.Roles);
        Assert.Equal(registered.Version, unchanged.Version);
    }

    [Theory]
    [InlineData("administrator")]
    [InlineData("employee")]
    public async Task PutRoles_ARoleTheErpDoesNotAssign_Answers400RequestMalformed(string role)
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await PutRolesAsync(client, registered.Id, [role], registered.Version);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.malformed", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task PutRoles_MoreRolesThanThereAre_AnswersAValidationProblemForTheRoles()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await PutRolesAsync(client, registered.Id, ["finance", "hr", "manager", "hr"], registered.Version);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Contains("roles", problem.Errors.Keys);
    }

    [Fact]
    public async Task PutRoles_UnknownPerson_Answers404UserNotFound()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await PutRolesAsync(client, Guid.CreateVersion7(), ["finance"], Convert.ToBase64String(new byte[8]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("user.not-found", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task Register_WithRoles_StoresThem()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        var registered = await PeopleApi.RegisterAsync(client, new { displayName = "Meera Nair", workEmail = PeopleApi.UniqueWorkEmail(), roles = Hr });

        Assert.Equal([PersonRole.Hr], registered.Roles);
    }

    [Fact]
    public async Task Register_WithRolesByACallerWhoMayNotAssignThem_Answers403AndStoresNoRecord()
    {
        await using var refusing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddScoped<ICallerPermissions, AllButAssigningRoles>()));
        using var client = refusing.CreateClient().AsUser(TestUsers.Administrator);
        var workEmail = PeopleApi.UniqueWorkEmail();

        using var response = await PeopleApi.PostAsync(client, new { displayName = "Meera Nair", workEmail, roles = Finance });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("request.forbidden", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        using var list = await client.GetAsync(PeopleApi.Path($"?filter={Uri.EscapeDataString($"workEmail:eq:{workEmail}")}"), TestContext.Current.CancellationToken);
        Assert.Empty((await PeopleApi.ReadPageAsync(list)).Items);
    }

    private static async Task<HttpResponseMessage> PutRolesAsync(HttpClient client, Guid id, string[] roles, string version)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, PeopleApi.Path($"/{id}/roles")) { Content = JsonContent.Create(new { roles, version }, options: PeopleApi.Json) };

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed class AllButAssigningRoles : ICallerPermissions
    {
        public bool Holds(string permission) => !string.Equals(permission, UsersPermissions.AssignRoles, StringComparison.Ordinal);
    }
}
