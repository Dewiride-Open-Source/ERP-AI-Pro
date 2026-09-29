using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Options;

// The __Host- prefix makes the browser refuse the cookie unless it is Secure, has path / and names no domain, so no
// sibling host can set or shadow it; without Expires it ends with the browser session.
internal sealed class SessionCookieOptionsSetup(IOptions<EntraSignInOptions> signIn) : IConfigureNamedOptions<CookieAuthenticationOptions>
{
    public const string CookieName = "__Host-erp-session";

    public void Configure(CookieAuthenticationOptions options) => Configure(Microsoft.Extensions.Options.Options.DefaultName, options);

    public void Configure(string? name, CookieAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(name, CookieAuthenticationDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.Cookie.Domain = null;
        options.ExpireTimeSpan = signIn.Value.SessionIdleTimeout;
        options.SlidingExpiration = true;
        options.EventsType = typeof(SessionCookieEvents);
    }
}
