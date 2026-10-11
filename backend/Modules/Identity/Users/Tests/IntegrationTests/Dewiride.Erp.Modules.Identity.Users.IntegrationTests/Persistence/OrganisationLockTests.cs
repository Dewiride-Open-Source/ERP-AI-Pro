using System.Net;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Persistence;

[Collection(OrganisationLockHolders.Name)]
public sealed class OrganisationLockTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task Writes_WhileAnotherChangeHoldsTheOrganisationLock_Return409AndChangeNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var renamed = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Finance"));
        var unused = await OrganisationApi.CreateDepartmentAsync(client, OrganisationApi.UniqueName("Unused"));
        var designation = await OrganisationApi.CreateDesignationAsync(client, OrganisationApi.UniqueName("Unused"));
        var person = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var leaving = await PeopleApi.RegisterAsync(client, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        var position = await OrganisationApi.RecordPositionAsync(client, person.Id, OrganisationApi.Today.AddDays(-10));
        var name = OrganisationApi.UniqueName("Audit");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var holder = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            await using var transaction = await holder.Database.BeginTransactionAsync(cancellationToken);
            Assert.True(await TransactionLocks.TryAcquireExclusiveAsync(holder, OrganisationLock.Resource, OrganisationLock.Timeout, cancellationToken));

            var responses = await Task.WhenAll(
                OrganisationApi.PostAsync(client, OrganisationApi.Departments(), new { name }),
                client.PutAsJsonAsync(OrganisationApi.Departments($"/{renamed.Id}"), new { name = OrganisationApi.UniqueName("Finance"), version = renamed.Version }, PeopleApi.Json, cancellationToken),
                client.DeleteAsync(OrganisationApi.Departments($"/{unused.Id}"), cancellationToken),
                client.DeleteAsync(OrganisationApi.Designations($"/{designation.Id}"), cancellationToken),
                OrganisationApi.RecordAsync(client, person.Id, OrganisationApi.Today),
                client.DeleteAsync(OrganisationApi.Positions(person.Id, $"/{position.Id}"), cancellationToken),
                client.DeleteAsync(PeopleApi.Path($"/{leaving.Id}"), cancellationToken));

            foreach (var response in responses)
            {
                using (response)
                {
                    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                    Assert.Equal("concurrency.conflict", await OrganisationApi.CodeOfAsync(response));
                }
            }

            await transaction.RollbackAsync(cancellationToken);
        }

        Assert.Equal(renamed, await ReadAsync<DepartmentResponse>(client, OrganisationApi.Departments($"/{renamed.Id}")));
        Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(client, OrganisationApi.Departments($"/{unused.Id}")));
        Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(client, OrganisationApi.Designations($"/{designation.Id}")));
        Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(client, PeopleApi.Path($"/{leaving.Id}")));
        Assert.Equal([position.Id], (await OrganisationApi.ReadPageAsync<PositionResponse>(client, OrganisationApi.Positions(person.Id))).Items.Select(p => p.Id));
        using var created = await OrganisationApi.PostAsync(client, OrganisationApi.Departments(), new { name });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    private static async Task<T> ReadAsync<T>(HttpClient client, Uri path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await OrganisationApi.ReadAsync<T>(response);
    }

    private static async Task<HttpStatusCode> StatusOfAsync(HttpClient client, Uri path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }
}
