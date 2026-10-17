using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.Permissions.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Permissions.Endpoints;

public sealed class PermissionEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string Route = "/api/identity/users/permissions";

    private static Uri Address => PeopleApi.Path("/permissions");

    [Fact]
    public void Route_ListPermissions_RequiresThePermissionToReadPeople()
    {
        Assert.Equal(UsersPermissions.Read, RoutePermissions.Of(factory.Services, "GET", Route));
    }

    [Fact]
    public async Task List_AsAdministrator_AnswersEveryRoleAndEveryPermissionOfTheCatalogue()
    {
        var catalog = factory.Services.GetRequiredService<PermissionCatalog>();
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        var answered = await ReadCatalogueAsync(client);

        Assert.Equal(Enum.GetValues<ErpRole>(), answered.Roles);
        Assert.Equal(
            catalog.Permissions.Select(permission => (permission.Name, permission.Description, string.Join(',', permission.HeldBy))),
            answered.Permissions.Select(permission => (permission.Name, permission.Description, string.Join(',', permission.HeldBy))));
    }

    [Fact]
    public async Task List_AsAdministrator_ListsThePermissionsInOrdinalOrderEachHeldByAdministrator()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        var answered = await ReadCatalogueAsync(client);

        Assert.NotEmpty(answered.Permissions);
        Assert.Equal(answered.Permissions.Select(permission => permission.Name).Order(StringComparer.Ordinal), answered.Permissions.Select(permission => permission.Name));
        Assert.All(answered.Permissions, permission => Assert.Contains(ErpRole.Administrator, permission.HeldBy));
    }

    [Fact]
    public async Task List_AsAdministrator_NamesTheRolesEachPermissionIsDeclaredFor()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        var answered = await ReadCatalogueAsync(client);

        Assert.Equal([ErpRole.Employee, ErpRole.Administrator], HoldersOf(answered, UsersPermissions.ReadDepartments));
        Assert.Equal([ErpRole.Employee, ErpRole.Administrator], HoldersOf(answered, UsersPermissions.ReadOrgChart));
        Assert.Equal([ErpRole.Administrator], HoldersOf(answered, UsersPermissions.Read));
        Assert.Equal([ErpRole.Administrator], HoldersOf(answered, UsersPermissions.AssignRoles));
    }

    [Fact]
    public async Task List_AsAdministrator_WritesTheRolesAsCamelCaseNames()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.GetAsync(Address, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(["employee", "finance", "hr", "manager", "administrator"], body.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        var departments = body.RootElement.GetProperty("permissions").EnumerateArray()
            .Single(permission => permission.GetProperty("name").GetString() == UsersPermissions.ReadDepartments);
        Assert.Equal(["employee", "administrator"], departments.GetProperty("heldBy").EnumerateArray().Select(role => role.GetString()));
    }

    [Fact]
    public async Task List_AsEmployee_Returns403()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(Address, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("request.forbidden", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task List_Anonymous_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Address, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_ModuleDisabled_AnswersFeatureDisabled()
    {
        using var disabled = new ErpApiFactory().WithFeature(UsersModule.FeatureFlag, enabled: false);
        using var client = disabled.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.GetAsync(Address, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await PeopleApi.ReadProblemAsync(response);
        Assert.Equal("feature.disabled", PeopleApi.CodeOf(problem));
        Assert.Equal(Route, problem.Instance);
    }

    private static async Task<PermissionCatalogResponse> ReadCatalogueAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Address, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var catalogue = await response.Content.ReadFromJsonAsync<PermissionCatalogResponse>(PeopleApi.Json, TestContext.Current.CancellationToken);
        Assert.NotNull(catalogue);

        return catalogue;
    }

    private static IReadOnlyList<ErpRole> HoldersOf(PermissionCatalogResponse catalogue, string permission) =>
        Assert.Single(catalogue.Permissions, candidate => candidate.Name == permission).HeldBy;
}
