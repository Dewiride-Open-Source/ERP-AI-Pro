using System.Threading.RateLimiting;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;

// Signed-in people are limited per person, not per address, so colleagues behind one office connection never share an
// allowance; only anonymous traffic is limited per client address.
internal static class GlobalRateLimiter
{
    public static PartitionedRateLimiter<HttpContext> Create(RateLimitingOptions options) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var actor = context.RequestServices.GetRequiredService<IActorContext>();
            if (actor.IsAuthenticated)
            {
                return RateLimitPartition.GetSlidingWindowLimiter($"actor:{actor.ActorId:N}", _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = options.ActorPermitLimit,
                    Window = options.ActorWindow,
                    SegmentsPerWindow = options.ActorSegmentsPerWindow,
                    QueueLimit = 0,
                });
            }

            return RateLimitPartition.GetFixedWindowLimiter($"address:{ClientAddressPartition.For(context.Connection.RemoteIpAddress)}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.AnonymousPermitLimit,
                Window = options.AnonymousWindow,
                QueueLimit = 0,
            });
        });
}
