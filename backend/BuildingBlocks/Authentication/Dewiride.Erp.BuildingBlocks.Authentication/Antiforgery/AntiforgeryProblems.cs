using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;

public static class AntiforgeryProblems
{
    public const string TokenMissing = "antiforgery.token-missing";

    public const string TokenInvalid = "antiforgery.token-invalid";

    public const string Title = "The request could not be confirmed as coming from this site.";

    public const string TokenMissingDetail = $"A request that changes data with the session cookie must carry the {AntiforgeryTokens.CookieName} cookie and the value of the {AntiforgeryTokens.RequestTokenCookieName} cookie in the {AntiforgeryTokens.HeaderName} header.";

    public const string TokenInvalidDetail = $"The antiforgery token does not belong to this session; GET {AuthPaths.Antiforgery} issues the signed-in person a new one.";

    internal static Task WriteAsync(HttpContext context, IProblemDetailsService problemDetails, string code)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        return problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = Title,
                Detail = string.Equals(code, TokenMissing, StringComparison.Ordinal) ? TokenMissingDetail : TokenInvalidDetail,
                Instance = context.Request.Path,
                Extensions = { [ResultExtensions.CodeExtension] = code },
            },
        }).AsTask();
    }
}
