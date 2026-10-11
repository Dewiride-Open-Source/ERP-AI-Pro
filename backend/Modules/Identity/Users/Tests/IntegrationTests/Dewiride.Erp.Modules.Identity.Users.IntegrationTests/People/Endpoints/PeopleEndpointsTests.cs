using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;

public sealed class PeopleEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task Register_AsAdministrator_Returns201WithTheLocationOfTheNormalisedRecord()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var workEmail = PeopleApi.UniqueWorkEmail();
        var employeeCode = PeopleApi.UniqueEmployeeCode();

        using var response = await PeopleApi.PostAsync(client, PeopleApi.Person(workEmail.ToUpperInvariant(), employeeCode));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var person = await PeopleApi.ReadPersonAsync(response);
        Assert.Equal(PeopleApi.Path($"/{person.Id}"), response.Headers.Location);
        Assert.Null(person.EntraObjectId);
        Assert.Equal("Meera Nair", person.DisplayName);
        Assert.Equal(workEmail, person.WorkEmail);
        Assert.Equal(employeeCode, person.EmployeeCode);
        Assert.Equal("+919876543210", person.PhoneNumber);
        Assert.Null(person.DepartmentId);
        Assert.Null(person.DesignationId);
        Assert.Null(person.ManagerId);
        Assert.Equal(new DateOnly(2026, 4, 1), person.DateOfJoining);
        Assert.Equal(PersonStatus.Active, person.Status);
        Assert.Null(person.LastSignedInAt);
        Assert.False(string.IsNullOrEmpty(person.Version));
    }

    [Fact]
    public async Task Register_SameIdempotencyKeyTwice_RegistersThePersonOnce()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var body = PeopleApi.Person(PeopleApi.UniqueWorkEmail());
        var key = Guid.CreateVersion7().ToString();

        using var first = await PeopleApi.PostAsync(client, body, key);
        using var second = await PeopleApi.PostAsync(client, body, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal((await PeopleApi.ReadPersonAsync(first)).Id, (await PeopleApi.ReadPersonAsync(second)).Id);
    }

    [Fact]
    public async Task Register_WithoutAnIdempotencyKey_Returns400WithoutRegistering()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var workEmail = PeopleApi.UniqueWorkEmail();

        using var response = await client.PostAsJsonAsync(PeopleApi.Path(), PeopleApi.Person(workEmail), PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("idempotency.key-missing", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        Assert.Empty((await ListAsync(client, $"filter={Uri.EscapeDataString($"workEmail:eq:{workEmail}")}")).Items);
    }

    [Fact]
    public async Task Register_WorkEmailThatIsNoEmail_Returns400KeyedByWorkEmail()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await PeopleApi.PostAsync(client, PeopleApi.Person("not an email"));

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Contains("workEmail", problem.Errors.Keys);
    }

    [Fact]
    public async Task Register_PhoneNumberThatIsNoNumber_Returns400KeyedByPhoneNumber()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var body = new { displayName = "Meera Nair", workEmail = PeopleApi.UniqueWorkEmail(), phoneNumber = "12345" };

        using var response = await PeopleApi.PostAsync(client, body);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Equal("user.phone-number-invalid", PeopleApi.CodeOf(problem));
        Assert.Equal("phoneNumber", Assert.Single(problem.Errors.Keys));
    }

    [Fact]
    public async Task Register_WorkEmailOfAnotherRecord_Returns409()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var workEmail = PeopleApi.UniqueWorkEmail();
        await PeopleApi.RegisterAsync(client, PeopleApi.Person(workEmail));

        using var response = await PeopleApi.PostAsync(client, PeopleApi.Person(workEmail.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.work-email-taken", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task Register_EmployeeCodeOfAnotherRecord_Returns409()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var employeeCode = PeopleApi.UniqueEmployeeCode();
        await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail(), employeeCode));

        using var response = await PeopleApi.PostAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail(), employeeCode.ToLowerInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.employee-code-taken", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task Register_ObjectIdInTheBody_RegistersByWorkEmailOnly()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var body = new { entraObjectId = Guid.CreateVersion7(), displayName = "Meera Nair", workEmail = PeopleApi.UniqueWorkEmail() };

        var person = await PeopleApi.RegisterAsync(client, body);

        Assert.Null(person.EntraObjectId);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "")]
    [InlineData("GET", "/{id}")]
    [InlineData("PUT", "/{id}")]
    [InlineData("PUT", "/{id}/status")]
    [InlineData("PUT", "/{id}/roles")]
    [InlineData("DELETE", "/{id}")]
    [InlineData("GET", "/directory?search=meera")]
    [InlineData("POST", "/invitations")]
    public async Task EveryRoute_AsPersonWithoutItsPermission_Returns403RequestForbidden(string method, string suffix)
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);
        using var request = new HttpRequestMessage(new HttpMethod(method), PeopleApi.Path(suffix.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)));
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { }, options: PeopleApi.Json);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("request.forbidden", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Theory]
    [InlineData("GET", "/api/identity/users", UsersPermissions.Read)]
    [InlineData("GET", "/api/identity/users/{id:guid}", UsersPermissions.Read)]
    [InlineData("POST", "/api/identity/users", UsersPermissions.Register)]
    [InlineData("GET", "/api/identity/users/directory", UsersPermissions.Register)]
    [InlineData("POST", "/api/identity/users/invitations", UsersPermissions.Register)]
    [InlineData("PUT", "/api/identity/users/{id:guid}", UsersPermissions.Update)]
    [InlineData("PUT", "/api/identity/users/{id:guid}/status", UsersPermissions.ChangeStatus)]
    [InlineData("PUT", "/api/identity/users/{id:guid}/roles", UsersPermissions.AssignRoles)]
    [InlineData("DELETE", "/api/identity/users/{id:guid}", UsersPermissions.Delete)]
    public void Route_OfEachUseCase_RequiresItsOwnPermission(string method, string route, string permission)
    {
        Assert.Equal(permission, RoutePermissions.Of(factory.Services, method, route));
    }

    [Fact]
    public async Task List_Anonymous_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(PeopleApi.Path(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.GetAsync(PeopleApi.Path($"/{Guid.CreateVersion7()}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("user.not-found", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task Get_RegisteredPerson_ReturnsTheRecordTheRegistrationAnswered()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await client.GetAsync(PeopleApi.Path($"/{registered.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equivalent(registered, await PeopleApi.ReadPersonAsync(response), strict: true);
    }

    [Fact]
    public async Task List_FilteredByWorkEmail_ReturnsOnlyThatPerson()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        var page = await ListAsync(client, $"filter={Uri.EscapeDataString($"workEmail:eq:{registered.WorkEmail}")}");

        Assert.Equivalent(registered, Assert.Single(page.Items), strict: true);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.Page);
    }

    [Fact]
    public async Task List_SortedByEmployeeCodeDescending_ReturnsThePeopleInThatOrder()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var probe = Probe();
        var first = await PeopleApi.RegisterAsync(client, new { displayName = "A", workEmail = PeopleApi.UniqueWorkEmail(), employeeCode = $"A-{probe}" });
        var second = await PeopleApi.RegisterAsync(client, new { displayName = "B", workEmail = PeopleApi.UniqueWorkEmail(), employeeCode = $"B-{probe}" });

        var page = await ListAsync(client, $"sort=employeeCode:desc&filter={Uri.EscapeDataString($"employeeCode:contains:{probe}")}");

        Assert.Equal(new[] { second.Id, first.Id }, page.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task List_FilteredByStatus_ReturnsOnlyPeopleOfThatStatus()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var probe = Probe();
        var active = await PeopleApi.RegisterAsync(client, new { displayName = "Active", workEmail = PeopleApi.UniqueWorkEmail(), employeeCode = $"A-{probe}" });
        var deactivated = await PeopleApi.RegisterAsync(client, new { displayName = "Deactivated", workEmail = PeopleApi.UniqueWorkEmail(), employeeCode = $"D-{probe}" });
        await ChangeStatusAsync(client, deactivated, "deactivated");

        var page = await ListAsync(client, $"filter={Uri.EscapeDataString($"employeeCode:contains:{probe};status:eq:active")}");

        Assert.Equal(active.Id, Assert.Single(page.Items).Id);
    }

    [Theory]
    [InlineData("?sort=salary:desc")]
    [InlineData("?filter=salary%3Aeq%3A1")]
    public async Task List_FieldItDoesNotSortOrFilterBy_Returns400(string query)
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.GetAsync(PeopleApi.Path(query), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_CurrentVersion_ChangesTheDetailsAndTheVersion()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var employeeCode = PeopleApi.UniqueEmployeeCode();

        using var response = await client.PutAsJsonAsync(
            PeopleApi.Path($"/{registered.Id}"),
            new { employeeCode, phoneNumber = "+44 20 7946 0958", dateOfJoining = "2025-12-01", version = registered.Version },
            PeopleApi.Json,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await PeopleApi.ReadPersonAsync(response);
        Assert.Equal(employeeCode, updated.EmployeeCode);
        Assert.Equal("+442079460958", updated.PhoneNumber);
        Assert.Equal(new DateOnly(2025, 12, 1), updated.DateOfJoining);
        Assert.Equal(registered.DisplayName, updated.DisplayName);
        Assert.Equal(registered.WorkEmail, updated.WorkEmail);
        Assert.NotEqual(registered.Version, updated.Version);
    }

    [Fact]
    public async Task Update_StaleVersion_Returns409AndKeepsTheNewerDetails()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        using (var first = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}"), new { phoneNumber = "+91 98765 00001", version = registered.Version }, PeopleApi.Json, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}"), new { phoneNumber = "+91 98765 00002", version = registered.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("+919876500001", (await GetAsync(client, registered.Id)).PhoneNumber);
    }

    [Fact]
    public async Task Update_VersionThatTheApiNeverIssued_Returns400KeyedByVersion()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}"), new { phoneNumber = "+91 98765 00003", version = "not a version" }, PeopleApi.Json, TestContext.Current.CancellationToken);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Equal("user.version-invalid", PeopleApi.CodeOf(problem));
        Assert.Equal("version", Assert.Single(problem.Errors.Keys));
    }

    [Fact]
    public async Task Update_WithoutAVersion_Returns400KeyedByVersion()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}"), new { phoneNumber = "+91 98765 00004" }, PeopleApi.Json, TestContext.Current.CancellationToken);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Contains("version", problem.Errors.Keys);
    }

    [Fact]
    public async Task Update_PhoneNumberThatIsNoNumber_Returns400KeyedByPhoneNumberAndKeepsTheRecord()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}"), new { phoneNumber = "12345", version = registered.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Equal("user.phone-number-invalid", PeopleApi.CodeOf(problem));
        Assert.Equal("phoneNumber", Assert.Single(problem.Errors.Keys));
        Assert.Equal(registered.PhoneNumber, (await GetAsync(client, registered.Id)).PhoneNumber);
    }

    [Fact]
    public async Task Update_EmployeeCodeOfAnotherRecord_Returns409()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var employeeCode = PeopleApi.UniqueEmployeeCode();
        await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail(), employeeCode));
        var other = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{other.Id}"), new { employeeCode, version = other.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.employee-code-taken", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{Guid.CreateVersion7()}"), new { version = "AAAAAAAAB9E=" }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_DeactivateThenReactivate_ReturnsTheRecordInEachStatus()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        var deactivated = await ChangeStatusAsync(client, registered, "deactivated");
        var reactivated = await ChangeStatusAsync(client, deactivated, "active");

        Assert.Equal(PersonStatus.Deactivated, deactivated.Status);
        Assert.Equal(PersonStatus.Active, reactivated.Status);
        Assert.NotEqual(deactivated.Version, reactivated.Version);
    }

    [Fact]
    public async Task ChangeStatus_UnknownStatus_Returns400()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}/status"), new { status = "suspended", version = registered.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithoutAStatus_Returns400KeyedByStatusAndLeavesTheRecordAsItWas()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await ChangeStatusAsync(client, registered, "deactivated");
        var deactivated = await GetAsync(client, registered.Id);

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}/status"), new { version = deactivated.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Contains("status", problem.Errors.Keys);
        Assert.Equal(PersonStatus.Deactivated, (await GetAsync(client, registered.Id)).Status);
    }

    [Fact]
    public async Task ChangeStatus_VersionThatTheApiNeverIssued_Returns400KeyedByVersion()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}/status"), new { status = "deactivated", version = "not a version" }, PeopleApi.Json, TestContext.Current.CancellationToken);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Equal("user.version-invalid", PeopleApi.CodeOf(problem));
        Assert.Equal(PersonStatus.Active, (await GetAsync(client, registered.Id)).Status);
    }

    [Fact]
    public async Task ChangeStatus_UnknownId_Returns404()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{Guid.CreateVersion7()}/status"), new { status = "deactivated", version = "AAAAAAAAB9E=" }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("user.not-found", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Fact]
    public async Task ChangeStatus_OwnRecord_Returns422AndLeavesItActive()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var own = await RecordOfAsync(client, TestUsers.Administrator);

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{own.Id}/status"), new { status = "deactivated", version = own.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("user.own-status", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        Assert.Equal(PersonStatus.Active, (await GetAsync(client, own.Id)).Status);
    }

    [Fact]
    public async Task Delete_RegisteredPerson_Returns204AndTheRecordIsGone()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await client.DeleteAsync(PeopleApi.Path($"/{registered.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var read = await client.GetAsync(PeopleApi.Path($"/{registered.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var row = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == UserId.From(registered.Id), TestContext.Current.CancellationToken);
        Assert.True(row.IsDeleted);
        Assert.Equal(TestUsers.Administrator.ObjectId, row.DeletedBy);
    }

    [Fact]
    public async Task Delete_ThenRegisterTheSameWorkEmail_RegistersANewRecord()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var workEmail = PeopleApi.UniqueWorkEmail();
        var deleted = await PeopleApi.RegisterAsync(client, PeopleApi.Person(workEmail));
        using (var response = await client.DeleteAsync(PeopleApi.Path($"/{deleted.Id}"), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(workEmail));

        Assert.NotEqual(deleted.Id, registered.Id);
    }

    [Fact]
    public async Task Delete_OwnRecord_Returns422AndKeepsIt()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var own = await RecordOfAsync(client, TestUsers.Administrator);

        using var response = await client.DeleteAsync(PeopleApi.Path($"/{own.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("user.own-deletion", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        Assert.Equal(own.Id, (await GetAsync(client, own.Id)).Id);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.DeleteAsync(PeopleApi.Path($"/{Guid.CreateVersion7()}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_FilteredByDepartmentSortedByDesignationDescending_ReturnsThePeopleInItToday()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var analyst = await OrganisationApi.CreateDesignationAsync(client, $"Analyst {Guid.CreateVersion7():N}");
        var controller = await OrganisationApi.CreateDesignationAsync(client, $"Controller {Guid.CreateVersion7():N}");
        var first = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var second = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var leaving = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, first.Id, OrganisationApi.Today.AddDays(-20), department.Id, analyst.Id);
        await OrganisationApi.RecordPositionAsync(client, second.Id, OrganisationApi.Today.AddDays(-20), department.Id, controller.Id);
        await OrganisationApi.RecordPositionAsync(client, leaving.Id, OrganisationApi.Today.AddDays(-20), department.Id, analyst.Id);
        await OrganisationApi.RecordPositionAsync(client, leaving.Id, OrganisationApi.Today.AddDays(-1));

        var page = await ListAsync(client, $"sort=designation:desc&filter={Uri.EscapeDataString($"departmentId:eq:{department.Id}")}");

        Assert.Equal([second.Id, first.Id], page.Items.Select(p => p.Id));
        Assert.Equal([controller.Name, analyst.Name], page.Items.Select(p => p.DesignationName));
    }

    [Fact]
    public async Task List_FilteredByDepartmentSortedByManager_ReturnsThePeopleByTheirManagersName()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Sales"));
        var zubin = await PeopleApi.RegisterAsync(client, new { displayName = $"Zubin {Guid.CreateVersion7():N}", workEmail = PeopleApi.UniqueWorkEmail() });
        var anand = await PeopleApi.RegisterAsync(client, new { displayName = $"Anand {Guid.CreateVersion7():N}", workEmail = PeopleApi.UniqueWorkEmail() });
        var first = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var second = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, first.Id, OrganisationApi.Today, department.Id, managerId: zubin.Id);
        await OrganisationApi.RecordPositionAsync(client, second.Id, OrganisationApi.Today, department.Id, managerId: anand.Id);

        var page = await ListAsync(client, $"sort=manager:asc&filter={Uri.EscapeDataString($"departmentId:eq:{department.Id}")}");

        Assert.Equal([second.Id, first.Id], page.Items.Select(p => p.Id));
        Assert.Equal([anand.Id, zubin.Id], page.Items.Select(p => p.ManagerId));
    }

    [Fact]
    public async Task List_FilteredByManager_ReturnsTheirReportsToday()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var manager = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var report = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var later = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, report.Id, OrganisationApi.Today.AddDays(-3), managerId: manager.Id);
        await OrganisationApi.RecordPositionAsync(client, later.Id, OrganisationApi.Today.AddDays(3), managerId: manager.Id);

        var page = await ListAsync(client, $"filter={Uri.EscapeDataString($"managerId:eq:{manager.Id}")}");

        Assert.Equal(report.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task List_FilteredByDesignationSortedByDepartment_ReturnsThePeopleWithItToday()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Auditor"));
        var audit = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Audit"));
        var tax = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Tax"));
        var inTax = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var inAudit = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var promoted = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, inTax.Id, OrganisationApi.Today.AddDays(-20), tax.Id, designation.Id);
        await OrganisationApi.RecordPositionAsync(client, inAudit.Id, OrganisationApi.Today.AddDays(-20), audit.Id, designation.Id);
        await OrganisationApi.RecordPositionAsync(client, promoted.Id, OrganisationApi.Today.AddDays(-20), audit.Id, designation.Id);
        await OrganisationApi.RecordPositionAsync(client, promoted.Id, OrganisationApi.Today.AddDays(-1), audit.Id);

        var page = await ListAsync(client, $"sort=department:asc&filter={Uri.EscapeDataString($"designationId:eq:{designation.Id}")}");

        Assert.Equal([inAudit.Id, inTax.Id], page.Items.Select(p => p.Id));
        Assert.Equal([audit.Name, tax.Name], page.Items.Select(p => p.DepartmentName));
    }

    [Fact]
    public async Task Delete_PersonSomeoneReportsToFromALaterDate_Returns409ManagesPeopleAndKeepsTheRecord()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var manager = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var report = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, report.Id, OrganisationApi.Today.AddDays(10), managerId: manager.Id);

        using var response = await client.DeleteAsync(PeopleApi.Path($"/{manager.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.manages-people", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        Assert.Equal(manager.Id, (await GetAsync(client, manager.Id)).Id);
    }

    [Fact]
    public async Task Delete_PersonSomeoneReportedToOnlyBeforeToday_Returns204()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var manager = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var report = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await OrganisationApi.RecordPositionAsync(client, report.Id, OrganisationApi.Today.AddDays(-30), managerId: manager.Id);
        await OrganisationApi.RecordPositionAsync(client, report.Id, OrganisationApi.Today);

        using var response = await client.DeleteAsync(PeopleApi.Path($"/{manager.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static string Probe() => Guid.CreateVersion7().ToString("N")[^12..];

    private static async Task<PagedResponse<PersonResponse>> ListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(PeopleApi.Path($"?{query}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await PeopleApi.ReadPageAsync(response);
    }

    private static async Task<PersonResponse> GetAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(PeopleApi.Path($"/{id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await PeopleApi.ReadPersonAsync(response);
    }

    private static async Task<PersonResponse> ChangeStatusAsync(HttpClient client, PersonResponse person, string status)
    {
        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{person.Id}/status"), new { status, version = person.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await PeopleApi.ReadPersonAsync(response);
    }

    private async Task<PersonResponse> RecordOfAsync(HttpClient client, TestUser user)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            if (!await context.Users.AnyAsync(u => u.EntraObjectId == user.ObjectId, TestContext.Current.CancellationToken))
            {
                context.Users.Add(User.Invite(user.ObjectId, user.Name, user.UserName, null, null, null, []).Value);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        await using var read = factory.Services.CreateAsyncScope();
        var id = await read.ServiceProvider.GetRequiredService<UsersDbContext>().Users
            .Where(u => u.EntraObjectId == user.ObjectId)
            .Select(u => u.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        return await GetAsync(client, id.Value);
    }
}
