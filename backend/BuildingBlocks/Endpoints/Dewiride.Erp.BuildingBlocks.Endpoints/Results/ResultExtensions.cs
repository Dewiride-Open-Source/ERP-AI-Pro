using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Results;

public static class ResultExtensions
{
    public const string CodeExtension = "code";

    public static Results<Ok<TValue>, ProblemHttpResult> ToHttpResult<TValue>(this Result<TValue> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : ToProblem(result.Error!);

    public static Results<NoContent, ProblemHttpResult> ToHttpResult(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : ToProblem(result.Error!);

    public static Results<Created<TValue>, ProblemHttpResult> ToCreatedResult<TValue>(this Result<TValue> result, Func<TValue, string> location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return result.IsSuccess ? TypedResults.Created(location(result.Value), result.Value) : ToProblem(result.Error!);
    }

    public static ProblemHttpResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { [CodeExtension] = error.Code };
        if (error.Kind == ErrorKind.Validation && error.Fields.Count > 0)
        {
            return TypedResults.Problem(new HttpValidationProblemDetails(error.Fields.ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal))
            {
                Detail = error.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = extensions,
            });
        }

        return TypedResults.Problem(detail: error.Message, statusCode: StatusFor(error.Kind), extensions: extensions);
    }

    private static int StatusFor(ErrorKind kind) =>
        kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.TooLarge => StatusCodes.Status413PayloadTooLarge,
            ErrorKind.UnsupportedType => StatusCodes.Status415UnsupportedMediaType,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
}
