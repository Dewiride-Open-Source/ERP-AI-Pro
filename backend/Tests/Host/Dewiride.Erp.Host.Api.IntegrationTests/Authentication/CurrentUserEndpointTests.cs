using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed class CurrentUserEndpointTests(CurrentUserEndpointTests.Fixture fixture) : IClassFixture<CurrentUserEndpointTests.Fixture>
{
    [Fact]
    public async Task Get_SignedInWithTheSessionCookie_ReturnsTheObjectIdNameUserNameAndRolesOfThePerson()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);

        using var response = await client.GetAsync(new Uri(AuthPaths.Me, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["id", "name", "userName", "roles"], body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(TestUsers.Administrator.ObjectId, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(TestUsers.Administrator.Name, body.RootElement.GetProperty("name").GetString());
        Assert.Equal(TestUsers.Administrator.UserName, body.RootElement.GetProperty("userName").GetString());
        Assert.Equal(TestUsers.Administrator.Roles, body.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
    }

    [Fact]
    public async Task Get_SignedInThroughTheTestHeader_ReturnsThatPerson()
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(new Uri(AuthPaths.Me, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(TestUsers.Accountant.ObjectId, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal([TestUsers.UserRole], body.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
    }

    [Fact]
    public async Task Get_Anonymously_AnswersUnauthenticatedWithoutALocation()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);

        using var response = await client.GetAsync(new Uri(AuthPaths.Me, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("WWW-Authenticate"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ProblemTypes.RequestUnauthenticated, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(AuthenticationProblems.UnauthenticatedTitle, body.RootElement.GetProperty("title").GetString());
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(TestSignIn.Map);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
