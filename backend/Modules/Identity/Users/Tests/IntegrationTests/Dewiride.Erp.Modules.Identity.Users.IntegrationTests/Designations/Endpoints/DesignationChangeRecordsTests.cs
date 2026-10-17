using System.Net;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Designations.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Designations.Endpoints;

public sealed class DesignationChangeRecordsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task Create_Designation_RecordsItsName()
    {
        using var client = Administrator();

        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Analyst"));

        var created = Assert.Single(await ChangeRecordsApi.OfAsync(client, designation.Id));
        Assert.Equal(ChangeOperation.Created, created.Operation);
        Assert.Equal(ChangeRecordsApi.DesignationsKey, created.EntityType);
        Assert.Equal(ChangeRecordsApi.DesignationsKey, created.SubjectType);
        Assert.Equal(designation.Name, created.SubjectName);
        Assert.Equal(new RecordedValue("name", ChangeValueKind.Text, null, designation.Name, null, null, null), Assert.Single(created.Changes));
    }

    [Fact]
    public async Task Rename_Designation_RecordsTheNameBeforeAndAfter()
    {
        using var client = Administrator();
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Analyst"));
        DesignationResponse renamed;
        using (var response = await client.PutAsJsonAsync(
            OrganisationApi.Designations($"/{designation.Id}"),
            new { name = OrganisationApi.UniqueName("Senior Analyst"), version = designation.Version },
            PeopleApi.Json,
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            renamed = await OrganisationApi.ReadAsync<DesignationResponse>(response);
        }

        var changes = await ChangeRecordsApi.OfAsync(client, designation.Id);

        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated], changes.Select(change => change.Operation));
        Assert.Equal(new RecordedValue("name", ChangeValueKind.Text, designation.Name, renamed.Name, null, null, null), Assert.Single(changes[1].Changes));
    }

    [Fact]
    public async Task Delete_Designation_RecordsTheDeletionAndStillNamesTheDesignation()
    {
        using var client = Administrator();
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Intern"));
        using (var response = await client.DeleteAsync(OrganisationApi.Designations($"/{designation.Id}"), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var changes = await ChangeRecordsApi.OfAsync(client, designation.Id);

        Assert.Equal([ChangeOperation.Created, ChangeOperation.Deleted], changes.Select(change => change.Operation));
        Assert.Empty(changes[1].Changes);
        Assert.Equal(designation.Name, changes[1].SubjectName);
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
