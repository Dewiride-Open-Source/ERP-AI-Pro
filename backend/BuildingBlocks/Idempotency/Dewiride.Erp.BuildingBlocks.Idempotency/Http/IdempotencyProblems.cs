using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.BuildingBlocks.Idempotency.Http;

public static class IdempotencyProblems
{
    public const string KeyMissing = "idempotency.key-missing";

    public const string KeyInvalid = "idempotency.key-invalid";

    public const string InProgress = "idempotency.in-progress";

    public const string KeyReused = "idempotency.key-reused";

    public const string ReplayUnavailable = "idempotency.replay-unavailable";

    internal static Task WriteAsync(HttpContext context, IProblemDetailsService problemDetails, int statusCode, string code, string detail)
    {
        context.Response.StatusCode = statusCode;

        return problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Detail = detail,
                Instance = context.Request.Path,
                Extensions = { ["code"] = code },
            },
        }).AsTask();
    }
}
