using Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Requests;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Endpoints;

internal static class AuthEndpoints
{
    public const string Tag = "Auth";

    public const string LoginRouteName = "Auth.Login";

    public const string LogoutRouteName = "Auth.Logout";

    public const string FetchModeHeader = "Sec-Fetch-Mode";

    public const string FetchDestinationHeader = "Sec-Fetch-Dest";

    public const string NotAPageMessage = "The sign-in starts only from a page the browser opens itself, never from an image, a frame or a script.";

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

    private static Results<RedirectHttpResult, ChallengeHttpResult, ProblemHttpResult> Login([AsParameters] LoginRequest login, HttpContext httpContext)
    {
        // Every challenge writes a correlation and a nonce cookie, so another site that made the browser request this path
        // from images or frames again and again could fill the person's cookies until this site refused their requests. A
        // browser marks a page it opens itself as navigate and document; a client that sends neither header is no browser.
        if (!IsOpenedAsAPage(httpContext.Request.Headers))
        {
            return Error.Validation(ProblemTypes.RequestInvalid, NotAPageMessage).ToProblem();
        }

        return httpContext.User.Identity?.IsAuthenticated == true
            ? TypedResults.LocalRedirect(login.LocalReturnUrl)
            : TypedResults.Challenge(new AuthenticationProperties { RedirectUri = login.LocalReturnUrl }, [OpenIdConnectDefaults.AuthenticationScheme]);
    }

    private static SignOutHttpResult Logout() =>
        TypedResults.SignOut(
            new AuthenticationProperties { RedirectUri = AuthPaths.LoginPage },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);

    private static bool IsOpenedAsAPage(IHeaderDictionary headers) =>
        IsAbsentOrExactly(headers[FetchModeHeader], "navigate") && IsAbsentOrExactly(headers[FetchDestinationHeader], "document");

    private static bool IsAbsentOrExactly(StringValues values, string expected) =>
        values.Count == 0 || (values.Count == 1 && string.Equals(values[0], expected, StringComparison.Ordinal));
}
