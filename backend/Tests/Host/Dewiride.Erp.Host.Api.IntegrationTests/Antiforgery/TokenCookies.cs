using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Antiforgery;

internal static class TokenCookies
{
    public const string SessionCookieName = "__Host-erp-session";

    public const string Cleared = "expires=Thu, 01 Jan 1970";

    public const string ChangesPath = "/__test/changes";

    public const string ProtectedChangesPath = "/__test/protected-changes";

    public static void MapSignInAndChanges(IEndpointRouteBuilder routes)
    {
        TestSignIn.Map(routes);
        routes.MapMethods(ChangesPath, ["GET", "HEAD", "OPTIONS", "TRACE", "POST", "PUT", "PATCH", "DELETE"], () => Results.NoContent()).AllowAnonymous();
        routes.MapPost(ProtectedChangesPath, () => Results.NoContent());
    }

    public static string? SetCookieOf(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.SingleOrDefault(cookie => cookie.StartsWith($"{name}=", StringComparison.Ordinal))
            : null;

    public static string ValueOf(HttpResponseMessage response, string name) =>
        Uri.UnescapeDataString((SetCookieOf(response, name) ?? throw new InvalidOperationException($"The response sets no {name} cookie.")).Split(';')[0][(name.Length + 1)..]);

    public static string RequestTokenOf(HttpResponseMessage response) => ValueOf(response, AntiforgeryTokens.RequestTokenCookieName);

    // A fresh client carries only the cookies given, as another browser holding copies of them would.
    public static async Task<HttpResponseMessage> SendWithAsync(WebApplicationFactory<Program> factory, HttpMethod method, string path, string cookies, string? requestToken)
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(factory);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", cookies);
        if (requestToken is not null)
        {
            request.Headers.Add(AntiforgeryTokens.HeaderName, requestToken);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static string CookieHeader(params (string Name, string Value)[] cookies) =>
        string.Join("; ", cookies.Select(cookie => $"{cookie.Name}={Uri.EscapeDataString(cookie.Value)}"));
}
