using System.Net;
using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Graph;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;

// The check reads only the records linked to an Entra account, so a test links the records it registered by work email the
// way a first sign-in links them, and disables or deletes an account by adding its person to the host's test directory with
// that state; every other account of the test database stays enabled there, so a check one test runs leaves the people of
// the others alone. A check whose answer holds no enabled account changes nothing, so a test whose own accounts are all
// closed links an enabled one besides.
internal static class AccountCheckApi
{
    public static Uri Path { get; } = PeopleApi.Path("/directory/account-check");

    public static async Task<Guid> LinkAsync(ErpApiFactory factory, Guid personId)
    {
        var objectId = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await context.Users.SingleAsync(u => u.Id == UserId.From(personId), TestContext.Current.CancellationToken);
        Assert.True(user.SignIn(objectId, user.DisplayName, user.WorkEmail, TimeProvider.System.GetUtcNow()).IsSuccess);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return objectId;
    }

    public static void MarkAccount(ErpApiFactory factory, Guid objectId, DirectoryAccountState state) =>
        factory.Services.GetRequiredService<TestDirectory>().Add(
            new TestDirectoryPerson(objectId, "Directory Person", $"person.{objectId:N}@dewiride.test", null, AccountState: state));

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client) =>
        await client.PostAsync(Path, content: null, TestContext.Current.CancellationToken);

    public static async Task<DirectoryAccountCheckResponse> CheckAsync(HttpClient administrator)
    {
        using var response = await PostAsync(administrator);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await OrganisationApi.ReadAsync<DirectoryAccountCheckResponse>(response);
    }
}
