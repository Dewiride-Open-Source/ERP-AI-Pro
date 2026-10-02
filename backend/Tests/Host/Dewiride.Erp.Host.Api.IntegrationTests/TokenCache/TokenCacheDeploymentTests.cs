using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Deployment;

namespace Dewiride.Erp.Host.Api.IntegrationTests.TokenCache;

public sealed class TokenCacheDeploymentTests
{
    [Fact]
    public async Task Get_WithASessionIssuedBeforeTheApiRestarted_IsSignedInAsThatPerson()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        string cookie;
        await using (var beforeRestart = Host(deployment))
        {
            using var client = TestSignIn.CreateClient(beforeRestart);
            using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
            cookie = SessionCookies.ValueOf(signIn);
        }

        await using var afterRestart = Host(deployment);
        using var response = await SessionCookies.GetObjectIdWithAsync(afterRestart, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestUsers.Accountant.ObjectId.ToString("D"), await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_OnAnotherInstanceOnceThePersonSignedOut_AnswersUnauthenticatedAndClearsTheCookie()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        await using var first = Host(deployment);
        _ = first.Services;
        await using var second = Host(deployment);
        using var client = TestSignIn.CreateClient(first);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var cookie = SessionCookies.ValueOf(signIn);
        using (var beforeSignOut = await SessionCookies.GetObjectIdWithAsync(second, cookie))
        {
            Assert.Equal(HttpStatusCode.OK, beforeSignOut.StatusCode);
        }

        using var signOut = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        using var response = await SessionCookies.GetObjectIdWithAsync(second, cookie);

        Assert.Equal(HttpStatusCode.Found, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(SessionCookies.Cleared, SessionCookies.SetCookieOf(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_WithASessionOfAnotherDeployment_AnswersUnauthenticated()
    {
        await using var issuing = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var other = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        using var client = TestSignIn.CreateClient(issuing);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        using var response = await SessionCookies.GetObjectIdWithAsync(other, SessionCookies.ValueOf(signIn));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static ErpApiFactory Host(TestDeployment deployment) =>
        new ErpApiFactory().WithDeployment(deployment).WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
}
