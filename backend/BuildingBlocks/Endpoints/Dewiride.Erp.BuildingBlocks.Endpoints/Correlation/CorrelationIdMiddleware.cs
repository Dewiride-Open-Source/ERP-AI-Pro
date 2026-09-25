using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;

internal sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string ActivityTagName = "erp.correlation_id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Resolve(context);
        context.Features.Set<ICorrelationIdFeature>(new CorrelationIdFeature(correlationId));
        Activity.Current?.SetTag(ActivityTagName, correlationId);
        context.Response.OnStarting(static state =>
        {
            var (response, id) = ((HttpResponse Response, string Id))state;
            response.Headers[CorrelationId.HeaderName] = id;

            return Task.CompletedTask;
        }, (context.Response, correlationId));

        using var scope = logger.BeginScope(new CorrelationIdScope(correlationId));

        await next(context).ConfigureAwait(false);
    }

    private static string Resolve(HttpContext context)
    {
        var supplied = context.Request.Headers[CorrelationId.HeaderName];
        if (supplied.Count == 1 && CorrelationId.IsWellFormed(supplied[0]))
        {
            return supplied[0]!;
        }

        return Activity.Current?.TraceId.ToHexString() ?? Guid.CreateVersion7().ToString("N");
    }
}
