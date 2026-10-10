using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
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
        Assert.Equal("Accountant", person.Designation);
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
    public async Task Register_EntraObjectIdOfAnotherRecord_Returns409()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var objectId = Guid.CreateVersion7();
        await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail(), entraObjectId: objectId));

        using var response = await PeopleApi.PostAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail(), entraObjectId: objectId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.entra-object-id-taken", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "")]
    [InlineData("GET", "/{id}")]
    [InlineData("PUT", "/{id}")]
    [InlineData("PUT", "/{id}/status")]
    [InlineData("DELETE", "/{id}")]
    public async Task EveryRoute_AsPersonWithoutTheAdministratorRole_Returns403(string method, string suffix)
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);
        using var request = new HttpRequestMessage(new HttpMethod(method), PeopleApi.Path(suffix.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)));
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { }, options: PeopleApi.Json);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
        Assert.Equal(registered, await PeopleApi.ReadPersonAsync(response));
    }

    [Fact]
    public async Task List_FilteredByWorkEmail_ReturnsOnlyThatPerson()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        var page = await ListAsync(client, $"filter={Uri.EscapeDataString($"workEmail:eq:{registered.WorkEmail}")}");

        Assert.Equal(registered, Assert.Single(page.Items));
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.Page);
    }

    [Fact]
    public async Task List_SortedByEmployeeCodeDescending_ReturnsThePeopleInThatOrder()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var designation = $"Probe {Guid.CreateVersion7():N}";
        var first = await PeopleApi.RegisterAsync(client, new { displayName = "A", workEmail = PeopleApi.UniqueWorkEmail(), employeeCode = $"A-{designation[^8..]}", designation });
        var second = await PeopleApi.RegisterAsync(client, new { displayName = "B", workEmail = PeopleApi.UniqueWorkEmail(), employeeCode = $"B-{designation[^8..]}", designation });

        var page = await ListAsync(client, $"sort=employeeCode:desc&filter={Uri.EscapeDataString($"designation:eq:{designation}")}");

        Assert.Equal(new[] { second.Id, first.Id }, page.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task List_FilteredByStatus_ReturnsOnlyPeopleOfThatStatus()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var designation = $"Probe {Guid.CreateVersion7():N}";
        var active = await PeopleApi.RegisterAsync(client, new { displayName = "Active", workEmail = PeopleApi.UniqueWorkEmail(), designation });
        var deactivated = await PeopleApi.RegisterAsync(client, new { displayName = "Deactivated", workEmail = PeopleApi.UniqueWorkEmail(), designation });
        await ChangeStatusAsync(client, deactivated, "deactivated");

        var page = await ListAsync(client, $"filter={Uri.EscapeDataString($"designation:eq:{designation};status:eq:active")}");

        Assert.Equal(active.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task List_UnknownSortField_Returns400()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.GetAsync(PeopleApi.Path("?sort=salary:desc"), TestContext.Current.CancellationToken);

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
            new { employeeCode, phoneNumber = "+44 20 7946 0958", designation = "Finance Manager", dateOfJoining = "2025-12-01", version = registered.Version },
            PeopleApi.Json,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await PeopleApi.ReadPersonAsync(response);
        Assert.Equal(employeeCode, updated.EmployeeCode);
        Assert.Equal("+442079460958", updated.PhoneNumber);
        Assert.Equal("Finance Manager", updated.Designation);
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
        using (var first = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}"), new { designation = "First edit", version = registered.Version }, PeopleApi.Json, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}"), new { designation = "Stale edit", version = registered.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("First edit", (await GetAsync(client, registered.Id)).Designation);
    }

    [Fact]
    public async Task Update_VersionThatTheApiNeverIssued_Returns400KeyedByVersion()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}"), new { designation = "Edit", version = "not a version" }, PeopleApi.Json, TestContext.Current.CancellationToken);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Equal("user.version-invalid", PeopleApi.CodeOf(problem));
        Assert.Equal("version", Assert.Single(problem.Errors.Keys));
    }

    [Fact]
    public async Task Update_WithoutAVersion_Returns400KeyedByVersion()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var registered = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        using var response = await client.PutAsJsonAsync(PeopleApi.Path($"/{registered.Id}"), new { designation = "Edit" }, PeopleApi.Json, TestContext.Current.CancellationToken);

        var problem = await PeopleApi.ReadValidationProblemAsync(response);
        Assert.Contains("version", problem.Errors.Keys);
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
                context.Users.Add(User.Register(user.ObjectId, user.Name, user.UserName, null, null, null, null).Value);
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
