using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.BuildingBlocks.Authentication;

// Every caller of the API is a script, the web app's server or another application, never a page that could follow a
// redirect to a sign-in form, so every scheme refuses with a problem and no Location header, whichever kind of caller it is.
public static class AuthenticationProblems
{
    public const string UnauthenticatedTitle = "Sign in to use this API.";

    public const string ForbiddenTitle = "The caller may not do this.";

    internal static Task WriteUnauthenticatedAsync(HttpContext context, IProblemDetailsService problemDetails) =>
        WriteAsync(context, problemDetails, StatusCodes.Status401Unauthorized, ProblemTypes.RequestUnauthenticated, UnauthenticatedTitle);

    internal static Task WriteForbiddenAsync(HttpContext context, IProblemDetailsService problemDetails) =>
        WriteAsync(context, problemDetails, StatusCodes.Status403Forbidden, ProblemTypes.RequestForbidden, ForbiddenTitle);

    private static async Task WriteAsync(HttpContext context, IProblemDetailsService problemDetails, int status, string code, string title)
    {
        context.Response.StatusCode = status;
        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { [ResultExtensions.CodeExtension] = code },
            },
        }).ConfigureAwait(false);
    }
}
