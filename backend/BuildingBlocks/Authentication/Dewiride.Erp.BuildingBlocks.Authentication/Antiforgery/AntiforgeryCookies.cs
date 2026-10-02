using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;

// Both cookies are always Secure under the __Host- prefix, so no sibling host can set or shadow them, and carry no Expires,
// so they end with the browser session like the session cookie. The cookie token stays HttpOnly and SameSite=Strict; the
// request token is readable by the page's scripts and SameSite=Lax.
internal sealed class AntiforgeryCookies(IAntiforgery antiforgery)
{
    private static readonly CookieOptions CookieTokenOptions = Options(httpOnly: true, SameSiteMode.Strict);

    private static readonly CookieOptions RequestTokenOptions = Options(httpOnly: false, SameSiteMode.Lax);

    private static readonly object ClearedItem = new();

    // The request token names the person it was issued to, who is not yet the request's user while a sign-in completes. A
    // valid cookie token the request carries is reused, because it names nobody; the Entra callback, a form post from
    // Entra's own site, carries no SameSite=Strict cookie, so every Entra sign-in writes a new one. A reused cookie token is
    // written again when a refused session cookie cleared the pair earlier in the same request, so the clearing cannot win.
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

        var cookieToken = tokens.CookieToken
            ?? (context.Items.ContainsKey(ClearedItem) ? context.Request.Cookies[AntiforgeryTokens.CookieName] : null);
        if (cookieToken is not null)
        {
            context.Response.Cookies.Append(AntiforgeryTokens.CookieName, cookieToken, CookieTokenOptions);
        }

        context.Response.Cookies.Append(AntiforgeryTokens.RequestTokenCookieName, tokens.RequestToken!, RequestTokenOptions);
    }

    public static void Clear(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Cookies.Delete(AntiforgeryTokens.CookieName, CookieTokenOptions);
        context.Response.Cookies.Delete(AntiforgeryTokens.RequestTokenCookieName, RequestTokenOptions);
        context.Items[ClearedItem] = true;
    }

    private static CookieOptions Options(bool httpOnly, SameSiteMode sameSite) =>
        new() { HttpOnly = httpOnly, Secure = true, SameSite = sameSite, Path = "/" };
}
