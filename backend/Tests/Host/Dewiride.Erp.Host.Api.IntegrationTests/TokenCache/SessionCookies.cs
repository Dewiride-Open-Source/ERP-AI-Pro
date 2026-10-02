using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.TokenCache;

internal static class SessionCookies
{
    public const string Name = "__Host-erp-session";

    public const string Cleared = "expires=Thu, 01 Jan 1970";

    public const string ObjectIdPath = "/__test/object-id";

    public static void MapSignInAndObjectId(IEndpointRouteBuilder routes)
    {
        TestSignIn.Map(routes);
        routes.MapGet(ObjectIdPath, (HttpContext context) => context.User.FindFirst(TestUser.ObjectIdClaim)?.Value);
    }

    public static string? SetCookieOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.SingleOrDefault(cookie => cookie.StartsWith($"{Name}=", StringComparison.Ordinal))
            : null;

    public static string ValueOf(HttpResponseMessage response) =>
        (SetCookieOf(response) ?? throw new InvalidOperationException("The response sets no session cookie.")).Split(';')[0][(Name.Length + 1)..];

    public static Task<HttpResponseMessage> GetObjectIdAsync(HttpClient client) =>
        client.GetAsync(new Uri(ObjectIdPath, UriKind.Relative), TestContext.Current.CancellationToken);

    // A fresh client carries only the cookie given, as another browser holding a copy of it would.
    public static async Task<HttpResponseMessage> GetObjectIdWithAsync(WebApplicationFactory<Program> factory, string cookieValue)
    {
        using var client = TestSignIn.CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, ObjectIdPath);
        request.Headers.Add("Cookie", $"{Name}={cookieValue}");

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
