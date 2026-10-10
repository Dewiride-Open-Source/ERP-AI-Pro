using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;

// The sign-in paths are reached before anyone is signed in, and the OpenID Connect handler answers its callbacks inside the
// authentication middleware, ahead of the global limiter, so they get a limit of their own per client address that runs
// before authentication; every other path passes through it unlimited. Routing also matches a path with a trailing slash, so
// the limit does too.
internal static class SignInRateLimiter
{
    public const string PartitionPrefix = "sign-in:";

    private const string OtherPaths = "other";

    public static PartitionedRateLimiter<HttpContext> Create(RateLimitingOptions options, IReadOnlyCollection<string> paths)
    {
        var limitedPaths = paths.Select(path => new PathString(path)).ToArray();

        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!IsLimited(context.Request.Path, limitedPaths))
            {
                return RateLimitPartition.GetNoLimiter(OtherPaths);
            }

            return RateLimitPartition.GetFixedWindowLimiter($"{PartitionPrefix}{ClientAddressPartition.For(context.Connection.RemoteIpAddress)}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.SignInPermitLimit,
                Window = options.SignInWindow,
                QueueLimit = 0,
            });
        });
    }

    private static bool IsLimited(PathString requestPath, PathString[] limitedPaths)
    {
        var path = new PathString(requestPath.Value?.TrimEnd('/'));

        return limitedPaths.Any(limited => path.Equals(limited, StringComparison.OrdinalIgnoreCase));
    }
}
