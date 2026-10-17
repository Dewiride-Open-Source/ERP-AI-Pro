using System.Net;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Departments.Endpoints;

public sealed class DepartmentChangeRecordsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task Create_DepartmentUnderAnother_RecordsItsNameAndItsParentByName()
    {
        using var client = Administrator();
        var parent = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));

        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Payables"), parent.Id);

        var created = Assert.Single(await ChangeRecordsApi.OfAsync(client, department.Id));
        Assert.Equal(ChangeOperation.Created, created.Operation);
        Assert.Equal(ChangeRecordsApi.DepartmentsKey, created.EntityType);
        Assert.Equal(department.Id, created.EntityId);
        Assert.Equal(ChangeRecordsApi.DepartmentsKey, created.SubjectType);
        Assert.Equal(department.Name, created.SubjectName);
        Assert.Equal(
            [
                new RecordedValue("name", ChangeValueKind.Text, null, department.Name, null, null, null),
                new RecordedValue("parentId", ChangeValueKind.Reference, null, parent.Id.ToString("D"), ChangeRecordsApi.DepartmentsKey, null, parent.Name),
            ],
            created.ValuesByProperty);
    }

    [Fact]
    public async Task Rename_Department_RecordsTheNameBeforeAndAfter()
    {
        using var client = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Payroll"));
        var renamed = await UpdateAsync(client, department, OrganisationApi.UniqueName("Salaries"), parentId: null);

        var changes = await ChangeRecordsApi.OfAsync(client, department.Id);

        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated], changes.Select(change => change.Operation));
        Assert.Equal(new RecordedValue("name", ChangeValueKind.Text, department.Name, renamed.Name, null, null, null), Assert.Single(changes[1].Changes));
        Assert.All(changes, change => Assert.Equal(renamed.Name, change.SubjectName));
    }

    [Fact]
    public async Task Move_DepartmentUnderAnother_RecordsBothParentsByName()
    {
        using var client = Administrator();
        var from = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var to = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Treasury"));
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Payables"), from.Id);
        await UpdateAsync(client, department, department.Name, to.Id);

        var changes = await ChangeRecordsApi.OfAsync(client, department.Id);

        Assert.Equal(
            new RecordedValue("parentId", ChangeValueKind.Reference, from.Id.ToString("D"), to.Id.ToString("D"), ChangeRecordsApi.DepartmentsKey, from.Name, to.Name),
            Assert.Single(changes[^1].Changes));
    }

    [Fact]
    public async Task Delete_Department_RecordsTheDeletionAndStillNamesTheDepartment()
    {
        using var client = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Archive"));
        using (var response = await client.DeleteAsync(OrganisationApi.Departments($"/{department.Id}"), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var changes = await ChangeRecordsApi.OfAsync(client, department.Id);

        Assert.Equal([ChangeOperation.Created, ChangeOperation.Deleted], changes.Select(change => change.Operation));
        Assert.Empty(changes[1].Changes);
        Assert.Equal(department.Name, changes[1].SubjectName);
    }

    private static async Task<DepartmentResponse> UpdateAsync(HttpClient client, DepartmentResponse department, string name, Guid? parentId)
    {
        using var response = await client.PutAsJsonAsync(
            OrganisationApi.Departments($"/{department.Id}"),
            new { name, parentId, version = department.Version },
            PeopleApi.Json,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await OrganisationApi.ReadAsync<DepartmentResponse>(response);
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
