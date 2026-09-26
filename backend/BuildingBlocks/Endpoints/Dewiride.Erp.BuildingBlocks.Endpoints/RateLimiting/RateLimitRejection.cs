using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;

// A rejected sliding-window lease in .NET 10 carries no RetryAfter, so the caller supplies an estimate used when the lease reports none.
internal static class RateLimitRejection
{
    public static async ValueTask WriteAsync(OnRejectedContext context, TimeSpan estimatedRetryAfter)
    {
        var httpContext = context.HttpContext;
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var reported) ? reported : estimatedRetryAfter;
        httpContext.Response.Headers.RetryAfter = RetryAfterSeconds(retryAfter).ToString(CultureInfo.InvariantCulture);

        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = StatusCodes.Status429TooManyRequests },
        }).ConfigureAwait(false);
    }

    public static long RetryAfterSeconds(TimeSpan retryAfter) => Math.Max(1, (long)Math.Ceiling(retryAfter.TotalSeconds));
}
