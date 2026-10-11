using System.Net;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
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
    private static DateOnly Today => OrganisationApi.Today;

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
        await DeactivateAsync(administrator, left.Id);
        using var employee = factory.CreateClient().AsUser(TestUsers.Accountant);

        var chart = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(employee, OrganisationApi.OrgChart(Team(manager.Id)));

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

        using var response = await client.GetAsync(OrganisationApi.OrgChart($"{Team(Guid.CreateVersion7())}&on={Today:yyyy-MM-dd}"), TestContext.Current.CancellationToken);

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

        var today = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(client, OrganisationApi.OrgChart(Team(planned.Id)));
        var later = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(client, OrganisationApi.OrgChart($"{Team(planned.Id)}&on={Today.AddDays(30):yyyy-MM-dd}"));
        var earlier = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(client, OrganisationApi.OrgChart($"{Team(current.Id)}&on={Today.AddDays(-1):yyyy-MM-dd}"));

        Assert.Empty(today.Items);
        Assert.Equal(person.Id, Assert.Single(later.Items).Id);
        Assert.Equal(person.Id, Assert.Single(earlier.Items).Id);
    }

    [Fact]
    public async Task Get_PersonWhoJoinsLater_IsLeftOutUntilTheirJoiningDate()
    {
        using var administrator = Administrator();
        var manager = await RegisterAsync(administrator, "Lead");
        var joiner = await PeopleApi.RegisterAsync(administrator, new { displayName = $"Joiner {Guid.CreateVersion7():N}", workEmail = PeopleApi.UniqueWorkEmail(), dateOfJoining = Today.AddDays(30) });
        await OrganisationApi.RecordPositionAsync(administrator, joiner.Id, Today, managerId: manager.Id);
        using var employee = factory.CreateClient().AsUser(TestUsers.Accountant);

        var today = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(employee, OrganisationApi.OrgChart(Team(manager.Id)));
        var dayBefore = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(administrator, OrganisationApi.OrgChart($"{Team(manager.Id)}&on={Today.AddDays(29):yyyy-MM-dd}"));
        var joiningDay = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(administrator, OrganisationApi.OrgChart($"{Team(manager.Id)}&on={Today.AddDays(30):yyyy-MM-dd}"));

        Assert.Empty(today.Items);
        Assert.Empty(dayBefore.Items);
        Assert.Equal(joiner.Id, Assert.Single(joiningDay.Items).Id);
    }

    [Fact]
    public async Task Get_ReportOfADeactivatedManager_KeepsNamingThemThoughTheChartLeavesThemOut()
    {
        using var client = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var manager = await RegisterAsync(client, "Lead");
        var report = await RegisterAsync(client, "Report");
        await OrganisationApi.RecordPositionAsync(client, manager.Id, Today.AddDays(-30), departmentId: department.Id);
        await OrganisationApi.RecordPositionAsync(client, report.Id, Today.AddDays(-30), departmentId: department.Id, managerId: manager.Id);
        await DeactivateAsync(client, manager.Id);

        var chart = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(client, OrganisationApi.OrgChart(InDepartment(department.Id)));

        Assert.Equal([new OrgChartPersonResponse(report.Id, report.DisplayName, department.Id, department.Name, null, null, manager.Id, manager.DisplayName)], chart.Items);
    }

    [Fact]
    public async Task Get_SortedByDepartmentDescending_ListsTheTeamInThatOrder()
    {
        using var client = Administrator();
        var alpha = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Alpha"));
        var beta = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Beta"));
        var manager = await RegisterAsync(client, "Lead");
        var abe = await RegisterAsync(client, "Abe");
        var zed = await RegisterAsync(client, "Zed");
        await OrganisationApi.RecordPositionAsync(client, abe.Id, Today.AddDays(-5), departmentId: alpha.Id, managerId: manager.Id);
        await OrganisationApi.RecordPositionAsync(client, zed.Id, Today.AddDays(-5), departmentId: beta.Id, managerId: manager.Id);

        var chart = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(client, OrganisationApi.OrgChart($"{Team(manager.Id)}&sort=department:desc"));

        Assert.Equal([zed.Id, abe.Id], chart.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task Get_PastDateAsAdministrator_LeavesOutAPersonDeactivatedSince()
    {
        using var client = Administrator();
        var manager = await RegisterAsync(client, "Lead");
        var stays = await RegisterAsync(client, "Stays");
        var left = await RegisterAsync(client, "Left");
        await OrganisationApi.RecordPositionAsync(client, stays.Id, Today.AddDays(-30), managerId: manager.Id);
        await OrganisationApi.RecordPositionAsync(client, left.Id, Today.AddDays(-30), managerId: manager.Id);
        await DeactivateAsync(client, left.Id);

        var chart = await OrganisationApi.ReadPageAsync<OrgChartPersonResponse>(client, OrganisationApi.OrgChart($"{Team(manager.Id)}&on={Today.AddDays(-10):yyyy-MM-dd}"));

        Assert.Equal(stays.Id, Assert.Single(chart.Items).Id);
    }

    [Theory]
    [InlineData("?sort=salary:desc")]
    [InlineData("?filter=salary%3Aeq%3A1")]
    public async Task Get_FieldItDoesNotSortOrFilterBy_Returns400(string query)
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(OrganisationApi.OrgChart(query), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(OrganisationApi.OrgChart(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string Team(Guid manager) => $"?filter={Uri.EscapeDataString($"managerId:eq:{manager}")}";

    private static string InDepartment(Guid department) => $"?filter={Uri.EscapeDataString($"departmentId:eq:{department}")}";

    private static Task<PersonResponse> RegisterAsync(HttpClient client, string name) =>
        PeopleApi.RegisterAsync(client, new { displayName = $"{name} {Guid.CreateVersion7():N}", workEmail = PeopleApi.UniqueWorkEmail() });

    private static async Task DeactivateAsync(HttpClient client, Guid id)
    {
        string version;
        using (var read = await client.GetAsync(PeopleApi.Path($"/{id}"), TestContext.Current.CancellationToken))
        {
            version = (await PeopleApi.ReadPersonAsync(read)).Version;
        }

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{id}/status"), new { status = "deactivated", version }, PeopleApi.Json, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
