using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;

namespace Dewiride.Erp.Testing.Authentication;

// Sends the antiforgery request token on every request that changes data, the way the web app does: the value of the
// readable cookie the API set, in the header. A request that already names a token keeps its own.
internal sealed class RequestTokenHandler(CookieContainer cookies) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (ChangesData(request.Method)
            && !request.Headers.Contains(AntiforgeryTokens.HeaderName)
            && request.RequestUri is { } address
            && cookies.GetCookies(address)[AntiforgeryTokens.RequestTokenCookieName] is { } token)
        {
            request.Headers.Add(AntiforgeryTokens.HeaderName, Uri.UnescapeDataString(token.Value));
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static bool ChangesData(HttpMethod method) =>
        method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch || method == HttpMethod.Delete;
}
