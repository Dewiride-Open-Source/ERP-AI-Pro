using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;

internal sealed class RateLimiterOptionsSetup(IOptions<RateLimitingOptions> options) : IConfigureOptions<RateLimiterOptions>
{
    public void Configure(RateLimiterOptions limiter)
    {
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        var actorSegment = options.Value.ActorWindow / options.Value.ActorSegmentsPerWindow;
        limiter.OnRejected = (context, _) => RateLimitRejection.WriteAsync(context, actorSegment);
        if (options.Value.Enabled)
        {
            limiter.GlobalLimiter = GlobalRateLimiter.Create(options.Value);
        }
    }
}
