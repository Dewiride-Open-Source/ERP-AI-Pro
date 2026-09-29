using Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Requests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Endpoints;

internal static class AuthEndpoints
{
    public const string Tag = "Auth";

    public const string LoginRouteName = "Auth.Login";

    public const string LogoutRouteName = "Auth.Logout";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(AuthPaths.Prefix)
            .WithTags(Tag)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        group.MapGet("/login", Login)
            .WithName(LoginRouteName)
            .WithSummary("Starts the Microsoft Entra sign-in and returns to the local path afterwards; a signed-in caller goes straight to that path.")
            .Produces(StatusCodes.Status302Found)
            .ProducesValidationProblem()
            .AllowAnonymous();

        // A cross-site form post carries no SameSite=Lax session cookie, so the fallback policy refuses it before anything is signed out.
        group.MapPost("/logout", Logout)
            .WithName(LogoutRouteName)
            .WithSummary("Ends the session and redirects to the Microsoft Entra end-session endpoint, which returns to the sign-in page.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static Results<RedirectHttpResult, ChallengeHttpResult> Login([AsParameters] LoginRequest login, HttpContext httpContext) =>
        httpContext.User.Identity?.IsAuthenticated == true
            ? TypedResults.LocalRedirect(login.LocalReturnUrl)
            : TypedResults.Challenge(new AuthenticationProperties { RedirectUri = login.LocalReturnUrl }, [OpenIdConnectDefaults.AuthenticationScheme]);

    private static SignOutHttpResult Logout() =>
        TypedResults.SignOut(
            new AuthenticationProperties { RedirectUri = AuthPaths.LoginPage },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
}
