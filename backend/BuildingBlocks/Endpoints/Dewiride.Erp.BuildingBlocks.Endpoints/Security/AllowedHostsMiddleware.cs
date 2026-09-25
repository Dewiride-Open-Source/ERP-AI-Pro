using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Security;

// The framework's own host filter runs ahead of the whole pipeline and answers in HTML; this check sits inside the
// correlation, header and problem middleware so a rejected host gets the same response shape as any other error.
internal sealed partial class AllowedHostsMiddleware(RequestDelegate next, IOptions<AllowedHostsOptions> options, ILogger<AllowedHostsMiddleware> logger)
{
    private readonly StringSegment[] _hosts = options.Value.Hosts.Contains(AllowedHostsOptions.AnyHost, StringComparer.Ordinal)
        ? []
        : [.. options.Value.Hosts.Select(host => new StringSegment(host))];

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        if (_hosts.Length == 0 || (context.Request.Host.HasValue && HostString.MatchesAny(context.Request.Host.Value, _hosts)))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        LogRejected(logger, context.Request.Host.Value ?? string.Empty);
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The request names a host this API does not serve.",
                Extensions = { [ResultExtensions.CodeExtension] = ProblemTypes.RequestHostNotAllowed },
            },
        }).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected a request for host {Host}, which is not an allowed host")]
    private static partial void LogRejected(ILogger logger, string host);
}
