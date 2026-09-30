using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Testing.Authentication;

// Every test host authenticates with this scheme by default. A request naming a test user in the header is that user; a
// request without the header falls through to the real session cookie, so cookie tests exercise the product handler.
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var header))
        {
            return Context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        var user = Guid.TryParse(header, out var objectId) ? TestUsers.Find(objectId) : null;

        return Task.FromResult(user is null
            ? AuthenticateResult.Fail($"{UserHeader} names no test user.")
            : AuthenticateResult.Success(new AuthenticationTicket(user.ToPrincipal(SchemeName), SchemeName)));
    }
}
