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

public sealed class TeamEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static DateOnly Today => OrganisationApi.Today;

    [Fact]
    public void Route_ListTeam_RequiresThePermissionToReadPositions()
    {
        Assert.Equal(UsersPermissions.ReadPositions, RoutePermissions.Of(factory.Services, "GET", "/api/identity/users/{id:guid}/team"));
    }

    [Fact]
    public async Task ListTeam_PersonNobodyReportsTo_ReturnsAnEmptyList()
    {
        using var client = Administrator();
        var person = await RegisterAsync(client, "Alone");

        Assert.Empty(await TeamOfAsync(client, person.Id));
    }

    [Fact]
    public async Task ListTeam_PeopleWhoReportToThePersonToday_ListsThemByDisplayName()
    {
        using var client = Administrator();
        var manager = await RegisterAsync(client, "Lead");
        var zara = await RegisterAsync(client, "Zara");
        var aditi = await RegisterAsync(client, "Aditi");
        await OrganisationApi.RecordPositionAsync(client, zara.Id, Today.AddDays(-30), managerId: manager.Id);
        await OrganisationApi.RecordPositionAsync(client, aditi.Id, Today, managerId: manager.Id);

        var team = await TeamOfAsync(client, manager.Id);

        Assert.Equal([new TeamMemberResponse(aditi.Id, aditi.DisplayName), new TeamMemberResponse(zara.Id, zara.DisplayName)], team);
    }

    [Fact]
    public async Task ListTeam_PersonWhoReportsToThePersonOnlyFromALaterDate_ListsThemAndLeavesOutWhoReportedOnlyBeforeToday()
    {
        using var client = Administrator();
        var manager = await RegisterAsync(client, "Lead");
        var other = await RegisterAsync(client, "Other");
        var joining = await RegisterAsync(client, "Joining");
        var leaving = await RegisterAsync(client, "Leaving");
        await OrganisationApi.RecordPositionAsync(client, joining.Id, Today.AddDays(-30), managerId: other.Id);
        await OrganisationApi.RecordPositionAsync(client, joining.Id, Today.AddDays(20), managerId: manager.Id);
        await OrganisationApi.RecordPositionAsync(client, leaving.Id, Today.AddDays(-30), managerId: manager.Id);
        await OrganisationApi.RecordPositionAsync(client, leaving.Id, Today.AddDays(-1), managerId: other.Id);

        var team = await TeamOfAsync(client, manager.Id);

        Assert.Equal([new TeamMemberResponse(joining.Id, joining.DisplayName)], team);
    }

    [Fact]
    public async Task ListTeam_PersonNoRecordHas_Returns404UserNotFound()
    {
        using var client = Administrator();

        using var response = await client.GetAsync(PeopleApi.Path($"/{Guid.CreateVersion7()}/team"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("user.not-found", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task ListTeam_AsEmployee_Returns403()
    {
        using var administrator = Administrator();
        var person = await RegisterAsync(administrator, "Lead");
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(PeopleApi.Path($"/{person.Id}/team"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("request.forbidden", await OrganisationApi.CodeOfAsync(response));
    }

    private static Task<PersonResponse> RegisterAsync(HttpClient client, string name) =>
        PeopleApi.RegisterAsync(client, new { displayName = $"{name} {Guid.CreateVersion7():N}", workEmail = PeopleApi.UniqueWorkEmail() });

    private static async Task<IReadOnlyList<TeamMemberResponse>> TeamOfAsync(HttpClient client, Guid person)
    {
        using var response = await client.GetAsync(PeopleApi.Path($"/{person}/team"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await OrganisationApi.ReadAsync<List<TeamMemberResponse>>(response);
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
