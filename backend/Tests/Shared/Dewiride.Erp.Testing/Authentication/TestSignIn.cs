using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web.Extensibility;
using Xunit;

namespace Dewiride.Erp.Testing.Authentication;

// Signs a test user in the way a completed Entra callback does: MSAL redeems a code for the person, at the test token
// endpoint, which puts their account in the token cache, and the cookie scheme issues the real session cookie, so a test
// gets a session the product accepts without a round trip to Entra. The cookie is Secure, so the client talks https to the
// test server, and it follows no redirect, since a sign-in or sign-out redirects to Entra.
public static class TestSignIn
{
    public const string PathPrefix = "/__test/sign-in";

    public static Uri BaseAddress { get; } = new("https://localhost");

    public static void Map(IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapPost($"{PathPrefix}/{{objectId:guid}}", async (Guid objectId, HttpContext context) =>
        {
            var user = TestUsers.Find(objectId);
            if (user is null)
            {
                return Results.NotFound();
            }

            var application = await ApplicationAsync(context.RequestServices);
            await application.AcquireTokenByAuthorizationCode([], TestTokenEndpoint.CodeFor(user)).ExecuteAsync(context.RequestAborted);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, user.ToPrincipal(CookieAuthenticationDefaults.AuthenticationScheme));

            return Results.NoContent();
        }).AllowAnonymous();
    }

    public static HttpClient CreateClient<TEntryPoint>(WebApplicationFactory<TEntryPoint> factory)
        where TEntryPoint : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = BaseAddress, AllowAutoRedirect = false });
    }

    public static async Task<HttpResponseMessage> SignInAsync(HttpClient client, TestUser user)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(user);

        var response = await client.PostAsync(new Uri($"{PathPrefix}/{user.ObjectId:D}", UriKind.Relative), content: null, TestContext.Current.CancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return response;
        }

        var status = (int)response.StatusCode;
        response.Dispose();

        throw new InvalidOperationException($"The test sign-in of {user.UserName} answered {status}; map it with ErpApiFactory.WithTestEndpoints(TestSignIn.Map).");
    }

    public static async Task<bool> IsAccountCachedAsync(IServiceProvider services, TestUser user)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(user);

        using var scope = services.CreateScope();
        var application = await ApplicationAsync(scope.ServiceProvider).ConfigureAwait(false);

        return await application.GetAccountAsync(user.AccountId).ConfigureAwait(false) is not null;
    }

    public static async Task ForgetAccountAsync(IServiceProvider services, TestUser user)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(user);

        using var scope = services.CreateScope();
        var application = await ApplicationAsync(scope.ServiceProvider).ConfigureAwait(false);
        var account = await application.GetAccountAsync(user.AccountId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The token cache holds no account for {user.UserName}; sign the person in with TestSignIn first.");

        await application.RemoveAsync(account).ConfigureAwait(false);
    }

    private static Task<IConfidentialClientApplication> ApplicationAsync(IServiceProvider services) =>
        services.GetRequiredService<IConfidentialClientApplicationProvider>().GetConfidentialClientApplicationAsync(OpenIdConnectDefaults.AuthenticationScheme);
}
