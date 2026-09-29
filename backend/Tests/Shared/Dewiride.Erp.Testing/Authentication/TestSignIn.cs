using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Dewiride.Erp.Testing.Authentication;

// Signs a test user in through the product cookie scheme, which is what a completed Entra callback does, so a test gets
// the real session cookie without a round trip to Entra. The cookie is Secure, so the client talks https to the test server,
// and it follows no redirect, since a sign-in or sign-out redirects to Entra.
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
}
