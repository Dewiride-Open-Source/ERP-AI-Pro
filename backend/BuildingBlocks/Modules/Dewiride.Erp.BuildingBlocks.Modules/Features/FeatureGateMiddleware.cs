using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;

namespace Dewiride.Erp.BuildingBlocks.Modules.Features;

internal sealed class FeatureGateMiddleware(RequestDelegate next)
{
    public const string DisabledCode = "feature.disabled";

    public async Task InvokeAsync(HttpContext context, IVariantFeatureManagerSnapshot features, IProblemDetailsService problemDetails)
    {
        var gate = context.GetEndpoint()?.Metadata.GetMetadata<RequireFeatureMetadata>();
        if (gate is null || await features.IsEnabledAsync(gate.FeatureName, context.RequestAborted).ConfigureAwait(false))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Detail = $"The feature '{gate.FeatureName}' is disabled.",
                Instance = context.Request.Path,
                Extensions = { ["code"] = DisabledCode },
            },
        }).ConfigureAwait(false);
    }
}
