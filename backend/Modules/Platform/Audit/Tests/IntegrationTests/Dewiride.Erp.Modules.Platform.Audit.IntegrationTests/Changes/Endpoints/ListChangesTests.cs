using System.Globalization;
using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.Audit.Contracts;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints;

public sealed class ListChangesTests(ListChangesTests.Fixture fixture) : IClassFixture<ListChangesTests.Fixture>
{
    [Theory]
    [InlineData("GET", "/api/platform/audit/changes", AuditPermissions.ReadChanges)]
    [InlineData("GET", "/api/platform/audit/changes/export", AuditPermissions.ExportChanges)]
    public void Route_OfEachUseCase_RequiresItsOwnPermission(string method, string route, string permission)
    {
        Assert.Equal(permission, RoutePermissions.Of(fixture.Factory.Services, method, route));
    }

    [Fact]
    public async Task Get_ChangesOfADepartment_AnswersThePagingEnvelopeNewestFirstWithTheirValues()
    {
        using var client = Administrator();
        var (department, renamed) = await CreateRenameAndDeleteAsync(client);

        using var response = await client.GetAsync(ChangesApi.Path($"?filter={ChangesApi.Filter($"subjectId:eq:{department.Id}")}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["items", "page", "pageSize", "totalCount", "totalPages"], body.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(1, body.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(50, body.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, body.RootElement.GetProperty("totalCount").GetInt64());
        Assert.Equal(1, body.RootElement.GetProperty("totalPages").GetInt32());
        var changes = body.RootElement.GetProperty("items").Deserialize<List<ChangeRecordResponse>>(ChangesApi.Json);
        Assert.NotNull(changes);
        Assert.Equal([ChangeOperation.Deleted, ChangeOperation.Updated, ChangeOperation.Created], changes.Select(change => change.Operation));
        Assert.All(changes, change =>
        {
            Assert.Equal(ChangesApi.DepartmentKey, change.EntityType);
            Assert.Equal(department.Id, change.EntityId);
            Assert.Equal(ChangesApi.DepartmentKey, change.SubjectType);
            Assert.Equal(department.Id, change.SubjectId);
            Assert.Equal(renamed.Name, change.SubjectName);
            Assert.Equal(ChangeActorKind.Person, change.ActorKind);
            Assert.Equal(TestUsers.Administrator.ObjectId, change.ActorId);
            Assert.False(string.IsNullOrEmpty(change.CorrelationId));
        });
        Assert.Empty(changes[0].Changes);
        Assert.Equal(new ChangedValueResponse("name", ChangeValueKind.Text, department.Name, renamed.Name, null, null, null), Assert.Single(changes[1].Changes));
        Assert.Equal(
            [
                new ChangedValueResponse("name", ChangeValueKind.Text, null, department.Name, null, null, null),
                new ChangedValueResponse("parentId", ChangeValueKind.Reference, null, null, ChangesApi.DepartmentKey, null, null),
            ],
            changes[2].Changes);
    }

    [Fact]
    public async Task Get_SortedOldestFirst_AnswersTheChangesInTheOrderTheyWereMade()
    {
        using var client = Administrator();
        var (department, _) = await CreateRenameAndDeleteAsync(client);

        var page = await ChangesApi.ListAsync(client, $"sort=occurredAt:asc&filter={ChangesApi.Filter($"subjectId:eq:{department.Id}")}");

        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated, ChangeOperation.Deleted], page.Items.Select(change => change.Operation));
    }

    [Fact]
    public async Task Get_SecondPageOfTwo_AnswersTheOldestChange()
    {
        using var client = Administrator();
        var (department, _) = await CreateRenameAndDeleteAsync(client);

        var page = await ChangesApi.ListAsync(client, $"page=2&pageSize=2&filter={ChangesApi.Filter($"subjectId:eq:{department.Id}")}");

        Assert.Equal(ChangeOperation.Created, Assert.Single(page.Items).Operation);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
    }

    [Fact]
    public async Task Get_EachFilterOfTheList_SelectsTheChangesItNames()
    {
        using var client = Administrator();
        var department = await ChangesApi.CreateDepartmentAsync(client, ChangesApi.UniqueName("Payroll"));
        string correlationId;
        using (var rename = await ChangesApi.PutDepartmentAsync(client, department, ChangesApi.UniqueName("Salaries"), parentId: null))
        {
            Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
            correlationId = ChangesApi.CorrelationIdOf(rename);
        }

        await ChangesApi.DeleteDepartmentAsync(client, department.Id);
        var updated = Assert.Single((await ChangesApi.ListOfAsync(client, department.Id, ";operation:eq:updated")).Items);
        var at = updated.OccurredAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        Assert.Equal(updated.Id, Assert.Single((await ListAsync(client, $"correlationId:eq:{correlationId}")).Items).Id);
        Assert.Equal(updated.Id, Assert.Single((await ListAsync(client, $"entityId:eq:{department.Id};operation:in:updated|restored")).Items).Id);
        Assert.Equal(updated.Id, Assert.Single((await ListAsync(client, $"subjectId:eq:{department.Id};occurredAt:gte:{at};occurredAt:lte:{at}")).Items).Id);
        Assert.Equal(
            [ChangeOperation.Deleted, ChangeOperation.Created],
            (await ListAsync(client, $"subjectId:eq:{department.Id};operation:ne:updated")).Items.Select(change => change.Operation));
        Assert.Equal(
            3,
            (await ListAsync(client, $"entityType:eq:{ChangesApi.DepartmentKey};subjectType:eq:{ChangesApi.DepartmentKey};subjectId:eq:{department.Id};actorId:eq:{TestUsers.Administrator.ObjectId}")).TotalCount);
        Assert.Empty((await ListAsync(client, $"subjectId:eq:{department.Id};actorId:eq:{ActorIds.System}")).Items);
    }

    [Fact]
    public async Task Get_DepartmentMovedUnderAnother_NamesTheDepartmentItRefersTo()
    {
        using var client = Administrator();
        var parent = await ChangesApi.CreateDepartmentAsync(client, ChangesApi.UniqueName("Finance"));
        var child = await ChangesApi.CreateDepartmentAsync(client, ChangesApi.UniqueName("Payables"));
        await ChangesApi.UpdateDepartmentAsync(client, child, child.Name, parent.Id);

        var moved = Assert.Single((await ChangesApi.ListOfAsync(client, child.Id, ";operation:eq:updated")).Items);

        Assert.Equal(
            new ChangedValueResponse("parentId", ChangeValueKind.Reference, null, parent.Id.ToString("D"), ChangesApi.DepartmentKey, null, parent.Name),
            Assert.Single(moved.Changes));
    }

    [Fact]
    public async Task Get_ChangeMadeByASignedInAdministrator_NamesThemAndTheirRequest()
    {
        var person = new TestUser(Guid.CreateVersion7(), "Kavya Menon", $"kavya.menon.{Guid.CreateVersion7():N}@example.com", [AppRoles.User, AppRoles.Administrator]);
        fixture.Factory.Services.GetRequiredService<TestTokenEndpoint>().Admit(person);
        using var session = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(session, person);
        TestDepartment department;
        string correlationId;
        using (var created = await ChangesApi.PostDepartmentAsync(session, ChangesApi.UniqueName("Treasury")))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            department = await ChangesApi.ReadDepartmentAsync(created);
            correlationId = ChangesApi.CorrelationIdOf(created);
        }

        using var administrator = Administrator();
        var change = Assert.Single((await ChangesApi.ListOfAsync(administrator, department.Id)).Items);

        Assert.Equal(ChangeActorKind.Person, change.ActorKind);
        Assert.Equal(person.ObjectId, change.ActorId);
        Assert.Equal(person.Name, change.ActorName);
        Assert.Equal(correlationId, change.CorrelationId);
        Assert.Equal(department.Name, change.SubjectName);
    }

    [Theory]
    [InlineData("sort=entityType:asc", "query.invalid-field")]
    [InlineData("sort=occurredAt:sideways", "query.invalid-sort")]
    [InlineData("filter=changes:eq:name", "query.invalid-field")]
    [InlineData("filter=entityId:contains:0199", "query.invalid-operator")]
    [InlineData("filter=occurredAt:gt:yesterday", "query.invalid-value")]
    [InlineData("filter=operation:eq:renamed", "query.invalid-value")]
    [InlineData("filter=subjectId", "query.invalid-filter")]
    [InlineData("page=0", "query.invalid-page")]
    [InlineData("pageSize=201", "query.invalid-page")]
    public async Task Get_QueryOutsideTheAllowLists_AnswersTheQueryErrorCode(string query, string code)
    {
        using var client = Administrator();

        using var response = await client.GetAsync(ChangesApi.Path($"?{query}"), TestContext.Current.CancellationToken);

        await ChangesApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/export")]
    public async Task Get_AsPersonWithoutThePermission_AnswersForbidden(string suffix)
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(ChangesApi.Path(suffix), TestContext.Current.CancellationToken);

        await ChangesApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "request.forbidden");
    }

    [Theory]
    [InlineData("")]
    [InlineData("/export")]
    public async Task Get_Anonymous_AnswersUnauthenticated(string suffix)
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(ChangesApi.Path(suffix), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task<PagedResponse<ChangeRecordResponse>> ListAsync(HttpClient client, string filter) =>
        ChangesApi.ListAsync(client, $"filter={ChangesApi.Filter(filter)}");

    private static async Task<(TestDepartment Department, TestDepartment Renamed)> CreateRenameAndDeleteAsync(HttpClient client)
    {
        var department = await ChangesApi.CreateDepartmentAsync(client, ChangesApi.UniqueName("Payroll"));
        var renamed = await ChangesApi.UpdateDepartmentAsync(client, department, ChangesApi.UniqueName("Salaries"), parentId: null);
        await ChangesApi.DeleteDepartmentAsync(client, department.Id);

        return (department, renamed);
    }

    private HttpClient Administrator() => fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(TestSignIn.Map);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
