using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Errors;

public static class ProblemTypes
{
    public const string Prefix = "/problems/";

    public const string RequestInvalid = "request.invalid";

    public const string RequestMalformed = "request.malformed";

    public const string RequestTooLarge = "request.too-large";

    public const string RequestCancelled = "request.cancelled";

    public const string ResourceNotFound = "resource.not-found";

    public const string ServerError = "server.error";

    public static string ToTypeUri(string code) => Prefix + code;

    public static string DefaultCode(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => RequestInvalid,
            StatusCodes.Status404NotFound => ResourceNotFound,
            StatusCodes.Status413PayloadTooLarge => RequestTooLarge,
            StatusCodes.Status499ClientClosedRequest => RequestCancelled,
            _ => ServerError,
        };
}
