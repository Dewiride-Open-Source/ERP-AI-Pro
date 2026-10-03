using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Authentication;

// The default authenticate scheme. A route that takes bearer tokens is signed in by the token alone and every other route by
// the session cookie alone, decided from the routed endpoint before the rate limiter runs, so a caller is limited as the
// person or application it signs in as, and the cookie handler never runs, slides or signs anyone out on a bearer route.
public static class RouteSignInScheme
{
    public const string Name = "Erp.RouteSignIn";

    internal static string Select(HttpContext context) =>
        TakesBearerTokens(context) ? JwtBearerDefaults.AuthenticationScheme : CookieAuthenticationDefaults.AuthenticationScheme;

    internal static bool TakesBearerTokens(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<BearerTokenRouteMetadata>() is not null;
}
