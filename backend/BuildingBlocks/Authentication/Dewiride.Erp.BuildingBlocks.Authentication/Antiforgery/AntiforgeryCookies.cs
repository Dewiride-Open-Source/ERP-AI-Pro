using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;

// Both cookies are always Secure under the __Host- prefix, so no sibling host can set or shadow them, and carry no Expires,
// so they end with the browser session like the session cookie. The cookie token stays HttpOnly and SameSite=Strict; the
// request token is readable by the page's scripts and SameSite=Lax, so a page the server renders after a navigation from
// another site, such as the return from Entra, can still embed it in a form.
internal sealed class AntiforgeryCookies(IAntiforgery antiforgery)
{
    private static readonly CookieOptions CookieTokenOptions = Options(httpOnly: true, SameSiteMode.Strict);

    private static readonly CookieOptions RequestTokenOptions = Options(httpOnly: false, SameSiteMode.Lax);

    // The request token names the person it was issued to, who is not yet the request's user while a sign-in completes. A
    // cookie token the browser already holds is kept: it names nobody, and only a request token issued for the signed-in
    // person passes the check.
    public void Issue(HttpContext context, ClaimsPrincipal person)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(person);

        var user = context.User;
        AntiforgeryTokenSet tokens;
        context.User = person;
        try
        {
            tokens = antiforgery.GetTokens(context);
        }
        finally
        {
            context.User = user;
        }

        if (tokens.CookieToken is { } cookieToken)
        {
            context.Response.Cookies.Append(AntiforgeryTokens.CookieName, cookieToken, CookieTokenOptions);
        }

        context.Response.Cookies.Append(AntiforgeryTokens.RequestTokenCookieName, tokens.RequestToken!, RequestTokenOptions);
    }

    public static void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(AntiforgeryTokens.CookieName, CookieTokenOptions);
        response.Cookies.Delete(AntiforgeryTokens.RequestTokenCookieName, RequestTokenOptions);
    }

    private static CookieOptions Options(bool httpOnly, SameSiteMode sameSite) =>
        new() { HttpOnly = httpOnly, Secure = true, SameSite = sameSite, Path = "/" };
}
