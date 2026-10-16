using System.Net;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Positions.Endpoints;

public sealed class PositionChangeRecordsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static readonly DateOnly StartOfTheYear = new(2026, 4, 1);

    [Fact]
    public async Task Record_Position_RecordsItsCreationUnderThePersonWithTheNamesOfWhatItNames()
    {
        using var client = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Analyst"));
        var manager = await PeopleApi.RegisterAsync(client, new { displayName = OrganisationApi.UniqueName("Anand"), workEmail = PeopleApi.UniqueWorkEmail() });
        var person = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));

        var position = await OrganisationApi.RecordPositionAsync(client, person.Id, StartOfTheYear, department.Id, designation.Id, manager.Id);

        var changes = await ChangeRecordsApi.OfAsync(client, person.Id);
        Assert.Equal(
            [(ChangeOperation.Created, ChangeRecordsApi.PeopleKey), (ChangeOperation.Created, ChangeRecordsApi.PositionsKey)],
            changes.Select(change => (change.Operation, change.EntityType)));
        var recorded = changes[1];
        Assert.Equal(position.Id, recorded.EntityId);
        Assert.Equal(ChangeRecordsApi.PeopleKey, recorded.SubjectType);
        Assert.Equal(person.Id, recorded.SubjectId);
        Assert.Equal(person.DisplayName, recorded.SubjectName);
        Assert.Equal(
            [
                new RecordedValue("departmentId", ChangeValueKind.Reference, null, department.Id.ToString("D"), ChangeRecordsApi.DepartmentsKey, null, department.Name),
                new RecordedValue("designationId", ChangeValueKind.Reference, null, designation.Id.ToString("D"), ChangeRecordsApi.DesignationsKey, null, designation.Name),
                new RecordedValue("effectiveFrom", ChangeValueKind.Date, null, "2026-04-01", null, null, null),
                new RecordedValue("managerId", ChangeValueKind.Reference, null, manager.Id.ToString("D"), ChangeRecordsApi.PeopleKey, null, manager.DisplayName),
                new RecordedValue("userId", ChangeValueKind.Reference, null, person.Id.ToString("D"), ChangeRecordsApi.PeopleKey, null, person.DisplayName),
            ],
            recorded.ValuesByProperty);
    }

    [Fact]
    public async Task Withdraw_Position_RecordsItsRemovalUnderThePersonWithTheValuesItHad()
    {
        using var client = Administrator();
        var department = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var person = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var position = await OrganisationApi.RecordPositionAsync(client, person.Id, StartOfTheYear, department.Id);
        using (var response = await client.DeleteAsync(OrganisationApi.Positions(person.Id, $"/{position.Id}"), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var changes = await ChangeRecordsApi.OfAsync(client, person.Id);

        var withdrawn = changes[^1];
        Assert.Equal(ChangeOperation.Deleted, withdrawn.Operation);
        Assert.Equal(ChangeRecordsApi.PositionsKey, withdrawn.EntityType);
        Assert.Equal(position.Id, withdrawn.EntityId);
        Assert.Equal(person.Id, withdrawn.SubjectId);
        Assert.Equal(
            [
                new RecordedValue("departmentId", ChangeValueKind.Reference, department.Id.ToString("D"), null, ChangeRecordsApi.DepartmentsKey, department.Name, null),
                new RecordedValue("designationId", ChangeValueKind.Reference, null, null, ChangeRecordsApi.DesignationsKey, null, null),
                new RecordedValue("effectiveFrom", ChangeValueKind.Date, "2026-04-01", null, null, null, null),
                new RecordedValue("managerId", ChangeValueKind.Reference, null, null, ChangeRecordsApi.PeopleKey, null, null),
                new RecordedValue("userId", ChangeValueKind.Reference, person.Id.ToString("D"), null, ChangeRecordsApi.PeopleKey, person.DisplayName, null),
            ],
            withdrawn.ValuesByProperty);
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
