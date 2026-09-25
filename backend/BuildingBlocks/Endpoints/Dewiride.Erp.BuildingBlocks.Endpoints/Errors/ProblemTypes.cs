using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Errors;

public static class ProblemTypes
{
    public const string Prefix = "/problems/";

    public const string TraceIdExtension = "traceId";

    public const string RequestInvalid = "request.invalid";

    public const string RequestMalformed = "request.malformed";

    public const string RequestUnauthenticated = "request.unauthenticated";

    public const string RequestForbidden = "request.forbidden";

    public const string RequestMethodNotAllowed = "request.method-not-allowed";

    public const string RequestTooLarge = "request.too-large";

    public const string RequestUnsupportedMediaType = "request.unsupported-media-type";

    public const string RequestRejected = "request.rejected";

    public const string RequestCancelled = "request.cancelled";

    public const string RateLimitExceeded = "rate-limit.exceeded";

    public const string ResourceNotFound = "resource.not-found";

    public const string ServiceUnavailable = "service.unavailable";

    public const string ServerError = "server.error";

    public static string ToTypeUri(string code) => Prefix + code;

    public static string DefaultCode(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => RequestInvalid,
            StatusCodes.Status401Unauthorized => RequestUnauthenticated,
            StatusCodes.Status403Forbidden => RequestForbidden,
            StatusCodes.Status404NotFound => ResourceNotFound,
            StatusCodes.Status405MethodNotAllowed => RequestMethodNotAllowed,
            StatusCodes.Status413PayloadTooLarge => RequestTooLarge,
            StatusCodes.Status415UnsupportedMediaType => RequestUnsupportedMediaType,
            StatusCodes.Status429TooManyRequests => RateLimitExceeded,
            StatusCodes.Status499ClientClosedRequest => RequestCancelled,
            StatusCodes.Status503ServiceUnavailable => ServiceUnavailable,
            >= StatusCodes.Status400BadRequest and < StatusCodes.Status500InternalServerError => RequestRejected,
            _ => ServerError,
        };
}
