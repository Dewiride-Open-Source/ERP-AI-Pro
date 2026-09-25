using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Errors;

internal static class ProblemDetailsCustomizer
{
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = status;
        if (problem.Extensions.TryGetValue(ResultExtensions.CodeExtension, out var code) && code is string existing)
        {
            problem.Type = ProblemTypes.ToTypeUri(existing);
        }
        else
        {
            var defaultCode = ProblemTypes.DefaultCode(status);
            problem.Extensions[ResultExtensions.CodeExtension] = defaultCode;
            problem.Type = ProblemTypes.ToTypeUri(defaultCode);
        }

        problem.Instance ??= context.HttpContext.Request.Path;
        problem.Extensions[ProblemTypes.TraceIdExtension] = context.HttpContext.Features.Get<ICorrelationIdFeature>()?.CorrelationId ?? context.HttpContext.TraceIdentifier;
    }
}
