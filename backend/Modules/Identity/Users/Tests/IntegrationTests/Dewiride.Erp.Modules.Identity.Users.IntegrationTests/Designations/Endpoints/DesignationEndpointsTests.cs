using System.Net;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
using Dewiride.Erp.Modules.Identity.Users.Designations.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Designations.Endpoints;

public sealed class DesignationEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Theory]
    [InlineData("GET", "/api/identity/users/designations", UsersPermissions.ReadDesignations)]
    [InlineData("GET", "/api/identity/users/designations/{id:guid}", UsersPermissions.ReadDesignations)]
    [InlineData("POST", "/api/identity/users/designations", UsersPermissions.CreateDesignations)]
    [InlineData("PUT", "/api/identity/users/designations/{id:guid}", UsersPermissions.UpdateDesignations)]
    [InlineData("DELETE", "/api/identity/users/designations/{id:guid}", UsersPermissions.DeleteDesignations)]
    public void Route_OfEachUseCase_RequiresItsOwnPermission(string method, string route, string permission)
    {
        Assert.Equal(permission, RoutePermissions.Of(factory.Services, method, route));
    }

    [Fact]
    public async Task Create_AsAdministrator_Returns201WithTheLocationOfTheTrimmedDesignation()
    {
        using var client = Administrator();
        var name = OrganisationApi.UniqueName("Senior Accountant");

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Designations(), new { name = $" {name}  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var designation = await OrganisationApi.ReadAsync<DesignationResponse>(response);
        Assert.Equal(OrganisationApi.Designations($"/{designation.Id}"), response.Headers.Location);
        Assert.Equal(name, designation.Name);
        Assert.False(string.IsNullOrEmpty(designation.Version));
    }

    [Fact]
    public async Task Create_NameAnotherDesignationHasInAnotherCase_Returns409NameTaken()
    {
        using var client = Administrator();
        var name = OrganisationApi.UniqueName("Accountant");
        await OrganisationApi.CreateDesignationAsync(client, name);

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Designations(), new { name = name.ToLowerInvariant() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("designation.name-taken", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Create_NameLongerThanTheLimit_Returns400KeyedByName()
    {
        using var client = Administrator();

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Designations(), new { name = new string('a', 101) });

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Contains("name", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_AsEmployee_Returns403()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await OrganisationApi.PostAsync(client, OrganisationApi.Designations(), new { name = OrganisationApi.UniqueName("Accountant") });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_AsEmployeeFilteredByName_ReturnsTheDesignation()
    {
        using var administrator = Administrator();
        var designation = await OrganisationApi.CreateDesignationAsync(administrator, OrganisationApi.UniqueName("Analyst"));
        using var employee = factory.CreateClient().AsUser(TestUsers.Accountant);

        var page = await OrganisationApi.ReadPageAsync<DesignationResponse>(employee, OrganisationApi.Designations($"?filter={Uri.EscapeDataString($"name:eq:{designation.Name}")}"));

        Assert.Equivalent(designation, Assert.Single(page.Items), strict: true);
    }

    [Fact]
    public async Task Update_Rename_ChangesTheNameEveryPositionShows()
    {
        using var client = Administrator();
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Accountant"));
        var person = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, person.Id, OrganisationApi.Today.AddDays(-30), designationId: designation.Id);
        var name = OrganisationApi.UniqueName("Chartered Accountant");

        using var response = await client.PutAsJsonAsync(OrganisationApi.Designations($"/{designation.Id}"), new { name, version = designation.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(name, (await OrganisationApi.ReadAsync<DesignationResponse>(response)).Name);
        using var read = await client.GetAsync(PeopleApi.Path($"/{person.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(name, (await PeopleApi.ReadPersonAsync(read)).DesignationName);
    }

    [Fact]
    public async Task Update_VersionTheApiNeverIssued_Returns400KeyedByVersion()
    {
        using var client = Administrator();
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Accountant"));

        using var response = await client.PutAsJsonAsync(OrganisationApi.Designations($"/{designation.Id}"), new { name = designation.Name, version = "not a version" }, PeopleApi.Json, TestContext.Current.CancellationToken);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Equal("designation.version-invalid", PeopleApi.CodeOf(problem));
        Assert.Equal("version", Assert.Single(problem.Errors.Keys));
    }

    [Fact]
    public async Task Delete_DesignationAFuturePositionNames_Returns409InUse()
    {
        using var client = Administrator();
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Team Lead"));
        var person = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, person.Id, OrganisationApi.Today.AddDays(45), designationId: designation.Id);

        using var response = await client.DeleteAsync(OrganisationApi.Designations($"/{designation.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("designation.in-use", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Delete_DesignationOnlyAnEndedPositionNamed_Returns204AndHistoryKeepsItsName()
    {
        using var client = Administrator();
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Trainee"));
        var person = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var ended = await OrganisationApi.RecordPositionAsync(client, person.Id, OrganisationApi.Today.AddDays(-90), designationId: designation.Id);
        await OrganisationApi.RecordPositionAsync(client, person.Id, OrganisationApi.Today.AddDays(-1));

        using var response = await client.DeleteAsync(OrganisationApi.Designations($"/{designation.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var history = await OrganisationApi.ReadPageAsync<PositionResponse>(client, OrganisationApi.Positions(person.Id));
        Assert.Equal(designation.Name, history.Items.Single(p => p.Id == ended.Id).DesignationName);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404NotFound()
    {
        using var client = Administrator();

        using var response = await client.DeleteAsync(OrganisationApi.Designations($"/{Guid.CreateVersion7()}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("designation.not-found", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Get_CreatedDesignation_ReturnsItAtItsLocation()
    {
        using var client = Administrator();
        using var created = await OrganisationApi.PostAsync(client, OrganisationApi.Designations(), new { name = OrganisationApi.UniqueName("Controller") });
        var designation = await OrganisationApi.ReadAsync<DesignationResponse>(created);

        using var response = await client.GetAsync(created.Headers.Location, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equivalent(designation, await OrganisationApi.ReadAsync<DesignationResponse>(response), strict: true);
    }

    [Theory]
    [InlineData("?sort=grade:asc")]
    [InlineData("?filter=grade%3Aeq%3A1")]
    public async Task List_FieldItDoesNotSortOrFilterBy_Returns400(string query)
    {
        using var client = Administrator();

        using var response = await client.GetAsync(OrganisationApi.Designations(query), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404NotFound()
    {
        using var client = Administrator();
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Accountant"));

        using var response = await client.PutAsJsonAsync(OrganisationApi.Designations($"/{Guid.CreateVersion7()}"), new { name = OrganisationApi.UniqueName("Analyst"), version = designation.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("designation.not-found", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Update_NameAnotherDesignationHas_Returns409NameTaken()
    {
        using var client = Administrator();
        var taken = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Accountant"));
        var other = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Analyst"));

        using var response = await client.PutAsJsonAsync(OrganisationApi.Designations($"/{other.Id}"), new { name = taken.Name.ToUpperInvariant(), version = other.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("designation.name-taken", await OrganisationApi.CodeOfAsync(response));
    }

    [Fact]
    public async Task Update_StaleVersion_Returns409AndKeepsTheNewerName()
    {
        using var client = Administrator();
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Accountant"));
        var newer = OrganisationApi.UniqueName("Senior Accountant");
        using (var first = await client.PutAsJsonAsync(OrganisationApi.Designations($"/{designation.Id}"), new { name = newer, version = designation.Version }, PeopleApi.Json, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var response = await client.PutAsJsonAsync(OrganisationApi.Designations($"/{designation.Id}"), new { name = OrganisationApi.UniqueName("Stale"), version = designation.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var read = await client.GetAsync(OrganisationApi.Designations($"/{designation.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(newer, (await OrganisationApi.ReadAsync<DesignationResponse>(read)).Name);
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
