using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;

// The framework's rate limiting middleware also applies the policy an endpoint names, and only the global _limiter's options
// define those policies, so a second instance of it refuses every endpoint that names one; this one applies the sign-in limit
// alone and answers a refused request as the global _limiter does.
internal sealed class SignInRateLimitingMiddleware(RequestDelegate next, IOptions<RateLimitingOptions> options, IReadOnlyCollection<string> paths)
{
    private readonly PartitionedRateLimiter<HttpContext> _limiter = SignInRateLimiter.Create(options.Value, paths);

    public async Task InvokeAsync(HttpContext context)
    {
        using var lease = _limiter.AttemptAcquire(context);
        if (lease.IsAcquired)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await RateLimitRejection.WriteAsync(new OnRejectedContext { HttpContext = context, Lease = lease }, options.Value.SignInWindow).ConfigureAwait(false);
    }
}
