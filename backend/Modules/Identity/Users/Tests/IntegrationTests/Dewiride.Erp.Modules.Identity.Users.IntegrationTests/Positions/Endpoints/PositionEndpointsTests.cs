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

public sealed class PositionEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static readonly DateOnly Today = OrganisationApi.Today;

    [Theory]
    [InlineData("GET", "/api/identity/users/{id:guid}/positions", UsersPermissions.ReadPositions)]
    [InlineData("GET", "/api/identity/users/{id:guid}/positions/{positionId:guid}", UsersPermissions.ReadPositions)]
    [InlineData("POST", "/api/identity/users/{id:guid}/positions", UsersPermissions.RecordPositions)]
    [InlineData("DELETE", "/api/identity/users/{id:guid}/positions/{positionId:guid}", UsersPermissions.WithdrawPositions)]
    public void Route_OfEachUseCase_RequiresItsOwnPermission(string method, string route, string permission)
    {
        Assert.Equal(permission, RoutePermissions.Of(factory.Services, method, route));
    }

    [Fact]
    public async Task Record_PastDate_BecomesThePositionThePersonHoldsToday()
    {
        using var client = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Accountant"));
        var manager = await RegisterAsync(client);
        var person = await RegisterAsync(client);

        using var response = await OrganisationApi.RecordAsync(client, person.Id, Today.AddDays(-30), department.Id, designation.Id, manager.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var position = await OrganisationApi.ReadAsync<PositionResponse>(response);
        Assert.Equal(OrganisationApi.Positions(person.Id, $"/{position.Id}"), response.Headers.Location);
        Assert.Equal(new PositionResponse(position.Id, Today.AddDays(-30), department.Id, department.Name, designation.Id, designation.Name, manager.Id, manager.DisplayName), position);
        var read = await GetPersonAsync(client, person.Id);
        Assert.Equal((department.Id, department.Name), (read.DepartmentId, read.DepartmentName));
        Assert.Equal((designation.Id, designation.Name), (read.DesignationId, read.DesignationName));
        Assert.Equal((manager.Id, manager.DisplayName), (read.ManagerId, read.ManagerDisplayName));
    }

    [Fact]
    public async Task Record_FutureDate_HoldsOnlyFromThatDateAndTheHistoryListsItFirst()
    {
        using var client = Administrator();
        var finance = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var sales = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Sales"));
        var person = await RegisterAsync(client);
        var current = await OrganisationApi.RecordPositionAsync(client, person.Id, Today.AddDays(-30), departmentId: finance.Id);

        var planned = await OrganisationApi.RecordPositionAsync(client, person.Id, Today.AddDays(30), departmentId: sales.Id);

        Assert.Equal(finance.Id, (await GetPersonAsync(client, person.Id)).DepartmentId);
        var history = await OrganisationApi.ReadPageAsync<PositionResponse>(client, OrganisationApi.Positions(person.Id));
        Assert.Equal([planned.Id, current.Id], history.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task Record_DateThePersonHasAPositionFrom_Returns409DateTaken()
    {
        using var client = Administrator();
        var person = await RegisterAsync(client);
        await OrganisationApi.RecordPositionAsync(client, person.Id, Today);

        using var response = await OrganisationApi.RecordAsync(client, person.Id, Today);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("position.date-taken", await OrganisationApi.CodeOfAsync(response));
    }

    [Theory]
    [InlineData("departmentId", "position.department-not-found")]
    [InlineData("designationId", "position.designation-not-found")]
    [InlineData("managerId", "position.manager-not-found")]
    public async Task Record_IdThatNamesNothing_Returns404WithTheCodeOfThatId(string member, string code)
    {
        using var client = Administrator();
        var person = await RegisterAsync(client);

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Positions(person.Id), new Dictionary<string, object> { ["effectiveFrom"] = Today, [member] = Guid.CreateVersion7() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(code, await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Record_ThePersonAsTheirOwnManager_Returns422SelfManaged()
    {
        using var client = Administrator();
        var person = await RegisterAsync(client);

        using var response = await OrganisationApi.RecordAsync(client, person.Id, Today, managerId: person.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("position.self-managed", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Record_ManagerWhoReportsToThePerson_Returns422ReportingCycle()
    {
        using var client = Administrator();
        var asha = await RegisterAsync(client);
        var dev = await RegisterAsync(client);
        await OrganisationApi.RecordPositionAsync(client, dev.Id, Today.AddDays(-30), managerId: asha.Id);

        using var response = await OrganisationApi.RecordAsync(client, asha.Id, Today.AddDays(-10), managerId: dev.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("position.reporting-cycle", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Record_CycleThatClosesOnlyOnALaterDate_Returns422ReportingCycle()
    {
        using var client = Administrator();
        var asha = await RegisterAsync(client);
        var dev = await RegisterAsync(client);
        await OrganisationApi.RecordPositionAsync(client, dev.Id, Today.AddDays(40), managerId: asha.Id);

        using var response = await OrganisationApi.RecordAsync(client, asha.Id, Today.AddDays(-10), managerId: dev.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("position.reporting-cycle", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Record_ChainThatReachedThePersonOnlyBeforeTheDate_Returns201()
    {
        using var client = Administrator();
        var asha = await RegisterAsync(client);
        var dev = await RegisterAsync(client);
        await OrganisationApi.RecordPositionAsync(client, dev.Id, Today.AddDays(-60), managerId: asha.Id);
        await OrganisationApi.RecordPositionAsync(client, dev.Id, Today.AddDays(-30));

        using var response = await OrganisationApi.RecordAsync(client, asha.Id, Today.AddDays(-10), managerId: dev.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Record_DeactivatedManagerForAPositionThatHoldsToday_Returns422ManagerInactive()
    {
        using var client = Administrator();
        var manager = await DeactivateAsync(client, await RegisterAsync(client));
        var person = await RegisterAsync(client);

        using var response = await OrganisationApi.RecordAsync(client, person.Id, Today.AddDays(-10), managerId: manager.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("position.manager-inactive", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Record_DeactivatedManagerForAPositionThatEndedBeforeToday_Returns201()
    {
        using var client = Administrator();
        var manager = await DeactivateAsync(client, await RegisterAsync(client));
        var person = await RegisterAsync(client);
        await OrganisationApi.RecordPositionAsync(client, person.Id, Today.AddDays(-5));

        using var response = await OrganisationApi.RecordAsync(client, person.Id, Today.AddDays(-60), managerId: manager.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Record_ManagerWhoseRecordIsDeleted_Returns404ManagerNotFound()
    {
        using var client = Administrator();
        var manager = await RegisterAsync(client);
        using (var deleted = await client.DeleteAsync(PeopleApi.Path($"/{manager.Id}"), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        var person = await RegisterAsync(client);

        using var response = await OrganisationApi.RecordAsync(client, person.Id, Today.AddDays(-60), managerId: manager.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("position.manager-not-found", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Record_ForAPersonNoRecordHas_Returns404UserNotFound()
    {
        using var client = Administrator();

        using var response = await OrganisationApi.RecordAsync(client, Guid.CreateVersion7(), Today);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("user.not-found", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Record_WithoutAnEffectiveDate_Returns400KeyedByEffectiveFrom()
    {
        using var client = Administrator();
        var person = await RegisterAsync(client);

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Positions(person.Id), new { departmentId = (Guid?)null });

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Contains("effectiveFrom", problem.Errors.Keys);
    }

    [Fact]
    public async Task Record_WithoutAnIdempotencyKey_Returns400()
    {
        using var client = Administrator();
        var person = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync(OrganisationApi.Positions(person.Id), new { effectiveFrom = Today }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("idempotency.key-missing", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Record_AsEmployee_Returns403()
    {
        using var administrator = Administrator();
        var person = await RegisterAsync(administrator);
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await OrganisationApi.RecordAsync(client, person.Id, Today);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_AsEmployee_Returns403()
    {
        using var administrator = Administrator();
        var person = await RegisterAsync(administrator);
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(OrganisationApi.Positions(person.Id), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Withdraw_TheLatestPosition_TheOneBeforeItHoldsAgain()
    {
        using var client = Administrator();
        var finance = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var sales = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Sales"));
        var person = await RegisterAsync(client);
        await OrganisationApi.RecordPositionAsync(client, person.Id, Today.AddDays(-60), departmentId: finance.Id);
        var latest = await OrganisationApi.RecordPositionAsync(client, person.Id, Today.AddDays(-10), departmentId: sales.Id);

        using var response = await client.DeleteAsync(OrganisationApi.Positions(person.Id, $"/{latest.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(finance.Id, (await GetPersonAsync(client, person.Id)).DepartmentId);
    }

    [Fact]
    public async Task Withdraw_PositionWhoseEarlierOneNamesADepartmentDeletedSince_Returns422PreviousNoLongerValid()
    {
        using var client = Administrator();
        var finance = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var person = await RegisterAsync(client);
        await OrganisationApi.RecordPositionAsync(client, person.Id, Today.AddDays(-60), departmentId: finance.Id);
        var latest = await OrganisationApi.RecordPositionAsync(client, person.Id, Today.AddDays(-10));
        using (var deleted = await client.DeleteAsync(OrganisationApi.Departments($"/{finance.Id}"), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        using var response = await client.DeleteAsync(OrganisationApi.Positions(person.Id, $"/{latest.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("position.previous-no-longer-valid", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Withdraw_PositionWhoseEarlierOneWouldCloseACycle_Returns422ReportingCycle()
    {
        using var client = Administrator();
        var asha = await RegisterAsync(client);
        var dev = await RegisterAsync(client);
        var meera = await RegisterAsync(client);
        await OrganisationApi.RecordPositionAsync(client, asha.Id, Today.AddDays(-60), managerId: dev.Id);
        var later = await OrganisationApi.RecordPositionAsync(client, asha.Id, Today.AddDays(-20), managerId: meera.Id);
        await OrganisationApi.RecordPositionAsync(client, dev.Id, Today.AddDays(-10), managerId: asha.Id);

        using var response = await client.DeleteAsync(OrganisationApi.Positions(asha.Id, $"/{later.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("position.reporting-cycle", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Withdraw_PositionOfAnotherPerson_Returns404PositionNotFound()
    {
        using var client = Administrator();
        var person = await RegisterAsync(client);
        var other = await RegisterAsync(client);
        var position = await OrganisationApi.RecordPositionAsync(client, other.Id, Today);

        using var response = await client.DeleteAsync(OrganisationApi.Positions(person.Id, $"/{position.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("position.not-found", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Withdraw_AsEmployee_Returns403()
    {
        using var administrator = Administrator();
        var person = await RegisterAsync(administrator);
        var position = await OrganisationApi.RecordPositionAsync(administrator, person.Id, Today);
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.DeleteAsync(OrganisationApi.Positions(person.Id, $"/{position.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static Task<PersonResponse> RegisterAsync(HttpClient client) =>
        PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

    private static async Task<PersonResponse> GetPersonAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(PeopleApi.Path($"/{id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await PeopleApi.ReadPersonAsync(response);
    }

    private static async Task<PersonResponse> DeactivateAsync(HttpClient client, PersonResponse person)
    {
        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{person.Id}/status"), new { status = "deactivated", version = person.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await PeopleApi.ReadPersonAsync(response);
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
