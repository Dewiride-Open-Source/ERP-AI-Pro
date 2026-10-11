using System.Net;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Positions.Endpoints;

// Every test reads only the people under a manager it created, because the chart lists every active person of the database.
public sealed class OrgChartEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static readonly DateOnly Today = OrganisationApi.Today;

    [Fact]
    public void Route_OrgChart_RequiresItsOwnPermission()
    {
        Assert.Equal(UsersPermissions.ReadOrgChart, RoutePermissions.Of(factory.Services, "GET", "/api/identity/users/org-chart"));
    }

    [Fact]
    public async Task Get_TodayAsEmployee_ListsTheActivePeopleWithTheirPositionsByName()
    {
        using var administrator = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(administrator, OrganisationApi.UniqueName("Finance"));
        var designation = await OrganisationApi.CreateDesignationAsync(administrator, OrganisationApi.UniqueName("Accountant"));
        var manager = await RegisterAsync(administrator, "Lead");
        var zara = await RegisterAsync(administrator, "Zara");
        var aditi = await RegisterAsync(administrator, "Aditi");
        var left = await RegisterAsync(administrator, "Left");
        await OrganisationApi.RecordPositionAsync(administrator, zara.Id, Today.AddDays(-30), department.Id, designation.Id, manager.Id);
        await OrganisationApi.RecordPositionAsync(administrator, aditi.Id, Today.AddDays(-5), managerId: manager.Id);
        await OrganisationApi.RecordPositionAsync(administrator, left.Id, Today.AddDays(-5), managerId: manager.Id);
        using (var deactivated = await administrator.PutAsJsonAsync(PeopleApi.Path($"/{left.Id}/status"), new { status = "deactivated", version = await VersionOfAsync(administrator, left.Id) }, PeopleApi.Json, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        }

        using var employee = factory.CreateClient().AsUser(TestUsers.Accountant);

        var chart = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(employee, OrganisationApi.OrgChart(Team(manager)));

        Assert.Equal(
            [
                new OrgChartPersonResponse(aditi.Id, aditi.DisplayName, null, null, null, null, manager.Id, manager.DisplayName),
                new OrgChartPersonResponse(zara.Id, zara.DisplayName, department.Id, department.Name, designation.Id, designation.Name, manager.Id, manager.DisplayName),
            ],
            chart.Items);
    }

    [Fact]
    public async Task Get_TodayNamedByDateAsEmployee_Returns200()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(OrganisationApi.OrgChart($"?on={Today:yyyy-MM-dd}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task Get_AnotherDateAsEmployee_Returns403(int days)
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(OrganisationApi.OrgChart($"?on={Today.AddDays(days):yyyy-MM-dd}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("request.forbidden", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Get_AnotherDateAsAdministrator_ShowsAPlannedMoveOnlyFromItsDate()
    {
        using var client = Administrator();
        var current = await RegisterAsync(client, "Current");
        var planned = await RegisterAsync(client, "Planned");
        var person = await RegisterAsync(client, "Mover");
        await OrganisationApi.RecordPositionAsync(client, person.Id, Today.AddDays(-10), managerId: current.Id);
        await OrganisationApi.RecordPositionAsync(client, person.Id, Today.AddDays(20), managerId: planned.Id);

        var today = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(client, OrganisationApi.OrgChart(Team(planned)));
        var later = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(client, OrganisationApi.OrgChart($"{Team(planned)}&on={Today.AddDays(30):yyyy-MM-dd}"));
        var earlier = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(client, OrganisationApi.OrgChart($"{Team(current)}&on={Today.AddDays(-1):yyyy-MM-dd}"));

        Assert.Empty(today.Items);
        Assert.Equal(person.Id, Assert.Single(later.Items).Id);
        Assert.Equal(person.Id, Assert.Single(earlier.Items).Id);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(OrganisationApi.OrgChart(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string Team(PersonResponse manager) => $"?filter={Uri.EscapeDataString($"managerId:eq:{manager.Id}")}";

    private static Task<PersonResponse> RegisterAsync(HttpClient client, string name) =>
        PeopleApi.RegisterAsync(client, new { displayName = $"{name} {Guid.CreateVersion7():N}", workEmail = PeopleApi.UniqueWorkEmail() });

    private static async Task<string> VersionOfAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(PeopleApi.Path($"/{id}"), TestContext.Current.CancellationToken);

        return (await PeopleApi.ReadPersonAsync(response)).Version;
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
