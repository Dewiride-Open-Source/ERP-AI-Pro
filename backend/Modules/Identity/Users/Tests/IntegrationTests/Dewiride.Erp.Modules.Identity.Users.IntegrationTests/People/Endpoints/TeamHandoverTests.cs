using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.TestHost;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;

public sealed class TeamHandoverTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static DateOnly Today => OrganisationApi.Today;

    [Fact]
    public async Task ChangeStatus_DeactivatingAManagerWithoutNamingWhoTheirTeamReportsTo_Returns422AndChangesNothing()
    {
        using var client = Administrator();
        var manager = await RegisterAsync(client, "Lead");
        var report = await RegisterAsync(client, "Report");
        var position = await OrganisationApi.RecordPositionAsync(client, report.Id, Today.AddDays(-30), managerId: manager.Id);

        using var response = await DeactivateAsync(client, manager);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("user.team-handover-required", await OrganisationApi.CodeOfAsync(response));
        Assert.Equal(PersonStatus.Active, (await GetAsync(client, manager.Id)).Status);
        Assert.Equal([position], await PositionsOfAsync(client, report.Id));
    }

    [Fact]
    public async Task ChangeStatus_DeactivatingAManagerNamingANewManager_MovesEveryMemberOnTheirOwnDatesAndDeactivatesThem()
    {
        using var client = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Accountant"));
        var manager = await RegisterAsync(client, "Lead");
        var successor = await RegisterAsync(client, "Successor");
        var other = await RegisterAsync(client, "Other");
        var longstanding = await RegisterAsync(client, "Longstanding");
        var startsToday = await RegisterAsync(client, "Starts Today");
        var joinsLater = await RegisterAsync(client, "Joins Later");
        var leftBefore = await RegisterAsync(client, "Left Before");
        var earlier = await OrganisationApi.RecordPositionAsync(client, longstanding.Id, Today.AddDays(-30), department.Id, designation.Id, manager.Id);
        var replacedToday = await OrganisationApi.RecordPositionAsync(client, startsToday.Id, Today, department.Id, managerId: manager.Id);
        var current = await OrganisationApi.RecordPositionAsync(client, joinsLater.Id, Today.AddDays(-30), managerId: other.Id);
        var planned = await OrganisationApi.RecordPositionAsync(client, joinsLater.Id, Today.AddDays(10), designationId: designation.Id, managerId: manager.Id);
        var leftHistory = await OrganisationApi.RecordPositionAsync(client, leftBefore.Id, Today.AddDays(-60), managerId: manager.Id);
        var leftNow = await OrganisationApi.RecordPositionAsync(client, leftBefore.Id, Today.AddDays(-10));

        using var response = await DeactivateAsync(client, manager, new { managerId = successor.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deactivated = await PeopleApi.ReadPersonAsync(response);
        Assert.Equal((PersonStatus.Deactivated, PersonDeactivationReason.Administrator), (deactivated.Status, deactivated.DeactivatedBecause));

        var longstandingPositions = await PositionsOfAsync(client, longstanding.Id);
        Assert.Equal(2, longstandingPositions.Count);
        Assert.Equal(new PositionResponse(longstandingPositions[0].Id, Today, department.Id, department.Name, designation.Id, designation.Name, successor.Id, successor.DisplayName), longstandingPositions[0]);
        Assert.Equal(earlier, longstandingPositions[1]);

        var replaced = Assert.Single(await PositionsOfAsync(client, startsToday.Id));
        Assert.NotEqual(replacedToday.Id, replaced.Id);
        Assert.Equal(new PositionResponse(replaced.Id, Today, department.Id, department.Name, null, null, successor.Id, successor.DisplayName), replaced);

        var joinsLaterPositions = await PositionsOfAsync(client, joinsLater.Id);
        Assert.Equal(2, joinsLaterPositions.Count);
        Assert.NotEqual(planned.Id, joinsLaterPositions[0].Id);
        Assert.Equal(new PositionResponse(joinsLaterPositions[0].Id, Today.AddDays(10), null, null, designation.Id, designation.Name, successor.Id, successor.DisplayName), joinsLaterPositions[0]);
        Assert.Equal(current, joinsLaterPositions[1]);

        Assert.Equal([leftNow, leftHistory], await PositionsOfAsync(client, leftBefore.Id));
        var moved = await GetAsync(client, longstanding.Id);
        Assert.Equal((successor.Id, (bool?)true), (moved.ManagerId, moved.ManagerActive));
    }

    [Fact]
    public async Task ChangeStatus_TeamHandedToNoOne_RecordsEachMembersPositionFromTodayWithoutAManager()
    {
        using var client = Administrator();
        var manager = await RegisterAsync(client, "Lead");
        var report = await RegisterAsync(client, "Report");
        var earlier = await OrganisationApi.RecordPositionAsync(client, report.Id, Today.AddDays(-30), managerId: manager.Id);

        using var response = await DeactivateAsync(client, manager, new { managerId = (Guid?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var positions = await PositionsOfAsync(client, report.Id);
        Assert.Equal(new PositionResponse(positions[0].Id, Today, null, null, null, null, null, null), positions[0]);
        Assert.Equal(earlier, positions[1]);
        var read = await GetAsync(client, report.Id);
        Assert.Equal(((Guid?)null, (bool?)null), (read.ManagerId, read.ManagerActive));
    }

    [Fact]
    public async Task ChangeStatus_TeamHandedOver_FilesTheWithdrawnAndTheRecordedPositionUnderTheMember()
    {
        using var client = Administrator();
        var manager = await RegisterAsync(client, "Lead");
        var successor = await RegisterAsync(client, "Successor");
        var report = await RegisterAsync(client, "Report");
        var replacedToday = await OrganisationApi.RecordPositionAsync(client, report.Id, Today, managerId: manager.Id);

        using (var response = await DeactivateAsync(client, manager, new { managerId = successor.Id }))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var positionChanges = (await ChangeRecordsApi.OfAsync(client, report.Id)).Where(change => change.EntityType == ChangeRecordsApi.PositionsKey).ToList();
        var recorded = Assert.Single(await PositionsOfAsync(client, report.Id));
        Assert.Equal(3, positionChanges.Count);
        Assert.Equal((ChangeOperation.Created, replacedToday.Id), (positionChanges[0].Operation, positionChanges[0].EntityId));
        Assert.Contains(positionChanges[1..], change => change.Operation == ChangeOperation.Deleted && change.EntityId == replacedToday.Id);
        var handedOver = Assert.Single(positionChanges[1..], change => change.Operation == ChangeOperation.Created);
        Assert.Equal(recorded.Id, handedOver.EntityId);
        Assert.Contains(handedOver.Changes, value => value.Property == "managerId" && value.After == successor.Id.ToString("D"));
    }

    [Theory]
    [InlineData("nobody", "user.team-manager-not-found")]
    [InlineData("deactivated", "user.team-manager-inactive")]
    [InlineData("themself", "user.team-manager-same-person")]
    [InlineData("member", "user.team-manager-in-team")]
    public async Task ChangeStatus_TeamHandedToSomeoneWhoCannotTakeIt_Returns422AndChangesNothing(string successorKind, string code)
    {
        using var client = Administrator();
        var manager = await RegisterAsync(client, "Lead");
        var report = await RegisterAsync(client, "Report");
        var position = await OrganisationApi.RecordPositionAsync(client, report.Id, Today.AddDays(-30), managerId: manager.Id);
        var successor = successorKind switch
        {
            "nobody" => Guid.CreateVersion7(),
            "deactivated" => (await ChangeStatusAsync(client, await RegisterAsync(client, "Left"), "deactivated")).Id,
            "themself" => manager.Id,
            _ => report.Id,
        };

        using var response = await DeactivateAsync(client, manager, new { managerId = successor });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, await OrganisationApi.CodeOfAsync(response));
        Assert.Equal(PersonStatus.Active, (await GetAsync(client, manager.Id)).Status);
        Assert.Equal([position], await PositionsOfAsync(client, report.Id));
    }

    [Fact]
    public async Task ChangeStatus_TeamHandedToSomeoneWhoReportsToAMember_Returns422ReportingCycleAndChangesNothing()
    {
        using var client = Administrator();
        var manager = await RegisterAsync(client, "Lead");
        var first = await RegisterAsync(client, "First");
        var second = await RegisterAsync(client, "Second");
        var successor = await RegisterAsync(client, "Successor");
        var firstPosition = await OrganisationApi.RecordPositionAsync(client, first.Id, Today.AddDays(-30), managerId: manager.Id);
        var secondPosition = await OrganisationApi.RecordPositionAsync(client, second.Id, Today.AddDays(-30), managerId: manager.Id);
        await OrganisationApi.RecordPositionAsync(client, successor.Id, Today.AddDays(-5), managerId: second.Id);

        using var response = await DeactivateAsync(client, manager, new { managerId = successor.Id });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("user.team-reporting-cycle", await OrganisationApi.CodeOfAsync(response));
        Assert.Equal(PersonStatus.Active, (await GetAsync(client, manager.Id)).Status);
        Assert.Equal([firstPosition], await PositionsOfAsync(client, first.Id));
        Assert.Equal([secondPosition], await PositionsOfAsync(client, second.Id));
    }

    [Theory]
    [InlineData(UsersPermissions.RecordPositions)]
    [InlineData(UsersPermissions.WithdrawPositions)]
    public async Task ChangeStatus_TeamNamedByACallerWithoutAPermissionOverPositions_Returns403AndChangesNothing(string withheld)
    {
        await using var refusing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddScoped<ICallerPermissions>(_ => new AllPermissionsBut(withheld))));
        using var client = refusing.CreateClient().AsUser(TestUsers.Administrator);
        var manager = await RegisterAsync(client, "Lead");
        var successor = await RegisterAsync(client, "Successor");
        var report = await RegisterAsync(client, "Report");
        var position = await OrganisationApi.RecordPositionAsync(client, report.Id, Today.AddDays(-30), managerId: manager.Id);

        using var response = await DeactivateAsync(client, manager, new { managerId = successor.Id });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("request.forbidden", await OrganisationApi.CodeOfAsync(response));
        Assert.Equal(PersonStatus.Active, (await GetAsync(client, manager.Id)).Status);
        Assert.Equal([position], await PositionsOfAsync(client, report.Id));
    }

    [Fact]
    public async Task ChangeStatus_TeamGivenWithAReactivation_Returns400KeyedByTeam()
    {
        using var client = Administrator();
        var person = await ChangeStatusAsync(client, await RegisterAsync(client, "Left"), "deactivated");

        using var response = await client.PutAsJsonAsync(
            PeopleApi.Path($"/{person.Id}/status"),
            new { status = "active", version = person.Version, team = new { managerId = (Guid?)null } },
            PeopleApi.Json,
            TestContext.Current.CancellationToken);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Equal("team", Assert.Single(problem.Errors.Keys));
        Assert.Equal(PersonStatus.Deactivated, (await GetAsync(client, person.Id)).Status);
    }

    [Fact]
    public async Task ChangeStatus_DeactivatingAPersonWithoutATeam_IgnoresTheManagerTheRequestNames()
    {
        using var client = Administrator();
        var person = await RegisterAsync(client, "Alone");

        using var response = await DeactivateAsync(client, person, new { managerId = Guid.CreateVersion7() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PersonStatus.Deactivated, (await PeopleApi.ReadPersonAsync(response)).Status);
    }

    [Fact]
    public async Task ChangeStatus_DeactivatingAPersonWhoseTeamMovedOnBeforeToday_NeedsNoHandover()
    {
        using var client = Administrator();
        var manager = await RegisterAsync(client, "Lead");
        var report = await RegisterAsync(client, "Report");
        await OrganisationApi.RecordPositionAsync(client, report.Id, Today.AddDays(-30), managerId: manager.Id);
        await OrganisationApi.RecordPositionAsync(client, report.Id, Today.AddDays(-1));

        using var response = await DeactivateAsync(client, manager);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> DeactivateAsync(HttpClient client, PersonResponse person, object? team = null) =>
        team is null
            ? client.PutAsJsonAsync(PeopleApi.Path($"/{person.Id}/status"), new { status = "deactivated", version = person.Version }, PeopleApi.Json, TestContext.Current.CancellationToken)
            : client.PutAsJsonAsync(PeopleApi.Path($"/{person.Id}/status"), new { status = "deactivated", version = person.Version, team }, PeopleApi.Json, TestContext.Current.CancellationToken);

    private static async Task<PersonResponse> ChangeStatusAsync(HttpClient client, PersonResponse person, string status)
    {
        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{person.Id}/status"), new { status, version = person.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await PeopleApi.ReadPersonAsync(response);
    }

    private static Task<PersonResponse> RegisterAsync(HttpClient client, string name) =>
        PeopleApi.RegisterAsync(client, new { displayName = $"{name} {Guid.CreateVersion7():N}", workEmail = PeopleApi.UniqueWorkEmail() });

    private static async Task<PersonResponse> GetAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(PeopleApi.Path($"/{id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await PeopleApi.ReadPersonAsync(response);
    }

    private static async Task<IReadOnlyList<PositionResponse>> PositionsOfAsync(HttpClient client, Guid person) =>
        (await OrganisationApi.ReadPageAsync<PositionResponse>(client, OrganisationApi.Positions(person))).Items;

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);

    private sealed class AllPermissionsBut(string withheld) : ICallerPermissions
    {
        public bool Holds(string permission) => !string.Equals(permission, withheld, StringComparison.Ordinal);
    }
}
