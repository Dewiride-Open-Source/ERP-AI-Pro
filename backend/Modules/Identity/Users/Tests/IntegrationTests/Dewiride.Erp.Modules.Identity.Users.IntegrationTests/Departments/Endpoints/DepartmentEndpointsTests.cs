using System.Net;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Departments.Endpoints;

public sealed class DepartmentEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Theory]
    [InlineData("GET", "/api/identity/users/departments", UsersPermissions.ReadDepartments)]
    [InlineData("GET", "/api/identity/users/departments/{id:guid}", UsersPermissions.ReadDepartments)]
    [InlineData("POST", "/api/identity/users/departments", UsersPermissions.CreateDepartments)]
    [InlineData("PUT", "/api/identity/users/departments/{id:guid}", UsersPermissions.UpdateDepartments)]
    [InlineData("DELETE", "/api/identity/users/departments/{id:guid}", UsersPermissions.DeleteDepartments)]
    public void Route_OfEachUseCase_RequiresItsOwnPermission(string method, string route, string permission)
    {
        Assert.Equal(permission, RoutePermissions.Of(factory.Services, method, route));
    }

    [Fact]
    public async Task Create_AsAdministrator_Returns201WithTheLocationOfTheTrimmedDepartment()
    {
        using var client = Administrator();
        var name = OrganisationApi.UniqueName("Finance");

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Departments(), new { name = $"  {name} " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var department = await OrganisationApi.ReadAsync<DepartmentResponse>(response);
        Assert.Equal(OrganisationApi.Departments($"/{department.Id}"), response.Headers.Location);
        Assert.Equal(name, department.Name);
        Assert.Null(department.ParentId);
        Assert.False(string.IsNullOrEmpty(department.Version));
    }

    [Fact]
    public async Task Create_UnderAnotherDepartment_NamesItAsTheParent()
    {
        using var client = Administrator();
        var parent = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));

        var child = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Payables"), parent.Id);

        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public async Task Create_NameASiblingHasInAnotherCase_Returns409NameTaken()
    {
        using var client = Administrator();
        var parent = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var name = OrganisationApi.UniqueName("Payables");
        await OrganisationApi.CreateDepartmentAsync(client, name, parent.Id);

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Departments(), new { name = name.ToUpperInvariant(), parentId = parent.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("department.name-taken", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Create_NameATopLevelDepartmentHas_Returns409NameTaken()
    {
        using var client = Administrator();
        var name = OrganisationApi.UniqueName("Sales");
        await OrganisationApi.CreateDepartmentAsync(client, name);

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Departments(), new { name });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("department.name-taken", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Create_NameAnotherParentsDepartmentHas_Returns201()
    {
        using var client = Administrator();
        var name = OrganisationApi.UniqueName("Operations");
        var finance = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var sales = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Sales"));
        await OrganisationApi.CreateDepartmentAsync(client, name, finance.Id);

        var second = await OrganisationApi.CreateDepartmentAsync(client, name, sales.Id);

        Assert.Equal(sales.Id, second.ParentId);
    }

    [Fact]
    public async Task Create_ParentNoDepartmentHas_Returns404ParentNotFound()
    {
        using var client = Administrator();

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Departments(), new { name = OrganisationApi.UniqueName("Payables"), parentId = Guid.CreateVersion7() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("department.parent-not-found", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Create_WithoutAName_Returns400KeyedByName()
    {
        using var client = Administrator();

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Departments(), new { name = (string?)null });

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Contains("name", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_AsEmployee_Returns403AndCreatesNothing()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);
        var name = OrganisationApi.UniqueName("Finance");

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Departments(), new { name });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var administrator = Administrator();
        var page = await OrganisationApi.ReadPageAsync<DepartmentResponse>(administrator, OrganisationApi.Departments($"?filter={Uri.EscapeDataString($"name:eq:{name}")}"));
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task List_AsEmployeeFilteredByParent_ReturnsTheDepartmentsUnderItByName()
    {
        using var administrator = Administrator();
        var parent = await OrganisationApi.CreateDepartmentAsync(administrator, OrganisationApi.UniqueName("Finance"));
        var receivables = await OrganisationApi.CreateDepartmentAsync(administrator, $"Receivables {Guid.CreateVersion7():N}", parent.Id);
        var payables = await OrganisationApi.CreateDepartmentAsync(administrator, $"Payables {Guid.CreateVersion7():N}", parent.Id);
        using var employee = factory.CreateClient().AsUser(TestUsers.Accountant);

        var page = await OrganisationApi.ReadPageAsync<DepartmentResponse>(employee, OrganisationApi.Departments($"?filter={Uri.EscapeDataString($"parentId:eq:{parent.Id}")}"));

        Assert.Equal([payables.Id, receivables.Id], page.Items.Select(d => d.Id));
    }

    [Fact]
    public async Task Update_RenameAndMove_ChangesBothAndTheVersion()
    {
        using var client = Administrator();
        var sales = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Sales"));
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Payables"));
        var name = OrganisationApi.UniqueName("Collections");

        using var response = await client.PutAsJsonAsync(OrganisationApi.Departments($"/{department.Id}"), new { name, parentId = sales.Id, version = department.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await OrganisationApi.ReadAsync<DepartmentResponse>(response);
        Assert.Equal(name, updated.Name);
        Assert.Equal(sales.Id, updated.ParentId);
        Assert.NotEqual(department.Version, updated.Version);
    }

    [Fact]
    public async Task Update_MoveUnderADepartmentWithinIt_Returns422WouldContainItself()
    {
        using var client = Administrator();
        var finance = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var payables = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Payables"), finance.Id);
        var invoices = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Invoices"), payables.Id);

        using var response = await client.PutAsJsonAsync(OrganisationApi.Departments($"/{finance.Id}"), new { name = finance.Name, parentId = invoices.Id, version = finance.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("department.would-contain-itself", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Update_StaleVersion_Returns409AndKeepsTheNewerName()
    {
        using var client = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var newer = OrganisationApi.UniqueName("Accounts");
        using (var first = await client.PutAsJsonAsync(OrganisationApi.Departments($"/{department.Id}"), new { name = newer, version = department.Version }, PeopleApi.Json, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var response = await client.PutAsJsonAsync(OrganisationApi.Departments($"/{department.Id}"), new { name = OrganisationApi.UniqueName("Stale"), version = department.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var read = await client.GetAsync(OrganisationApi.Departments($"/{department.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(newer, (await OrganisationApi.ReadAsync<DepartmentResponse>(read)).Name);
    }

    [Fact]
    public async Task Delete_DepartmentWithADepartmentWithinIt_Returns409InUse()
    {
        using var client = Administrator();
        var finance = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Payables"), finance.Id);

        using var response = await client.DeleteAsync(OrganisationApi.Departments($"/{finance.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("department.in-use", await OrganisationApi.CodeOfAsync(response));
    }

    [Theory]
    [InlineData(-30)]
    [InlineData(30)]
    public async Task Delete_DepartmentAPositionNamesTodayOrLater_Returns409InUse(int startsInDays)
    {
        using var client = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var person = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, person.Id, OrganisationApi.Today.AddDays(startsInDays), departmentId: department.Id);

        using var response = await client.DeleteAsync(OrganisationApi.Departments($"/{department.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("department.in-use", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Delete_DepartmentOnlyAnEndedPositionNamed_Returns204AndHistoryKeepsItsName()
    {
        using var client = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var person = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var ended = await OrganisationApi.RecordPositionAsync(client, person.Id, OrganisationApi.Today.AddDays(-60), departmentId: department.Id);
        await OrganisationApi.RecordPositionAsync(client, person.Id, OrganisationApi.Today.AddDays(-10));

        using var response = await client.DeleteAsync(OrganisationApi.Departments($"/{department.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var read = await client.GetAsync(OrganisationApi.Departments($"/{department.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        var history = await OrganisationApi.ReadPageAsync<PositionResponse>(client, OrganisationApi.Positions(person.Id));
        Assert.Equal(department.Name, history.Items.Single(p => p.Id == ended.Id).DepartmentName);
    }

    [Fact]
    public async Task Delete_DepartmentOnlyADeletedPersonsPositionNames_Returns204()
    {
        using var client = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var person = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, person.Id, OrganisationApi.Today.AddDays(-10), departmentId: department.Id);
        using (var deleted = await client.DeleteAsync(PeopleApi.Path($"/{person.Id}"), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        using var response = await client.DeleteAsync(OrganisationApi.Departments($"/{department.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
