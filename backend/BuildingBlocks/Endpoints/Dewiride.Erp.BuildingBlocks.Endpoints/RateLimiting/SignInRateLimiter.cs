using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;

// The sign-in paths are reached before anyone is signed in, and the OpenID Connect handler answers its callbacks inside the
// authentication middleware, ahead of the global limiter, so they get a limit of their own that runs before authentication;
// every other path passes through it unlimited. Each path has its own allowance per client address, so the sign-ins of a
// busy office behind one address never refuse its sign-outs. Routing also matches a path with a trailing slash, so the limit
// does too.
internal static class SignInRateLimiter
{
    public const string PartitionPrefix = "sign-in:";

    private const string OtherPaths = "other";

    public static PartitionedRateLimiter<HttpContext> Create(RateLimitingOptions options, IReadOnlyCollection<string> paths)
    {
        var limitedPaths = paths.Select(path => new PathString(path)).ToArray();

        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (LimitedPathOf(context.Request.Path, limitedPaths) is not { } limited)
            {
                return RateLimitPartition.GetNoLimiter(OtherPaths);
            }

            return RateLimitPartition.GetFixedWindowLimiter($"{PartitionPrefix}{limited.Value}:{ClientAddressPartition.For(context.Connection.RemoteIpAddress)}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.SignInPermitLimit,
                Window = options.SignInWindow,
                QueueLimit = 0,
            });
        });
    }

    private static PathString? LimitedPathOf(PathString requestPath, PathString[] limitedPaths)
    {
        var path = new PathString(requestPath.Value?.TrimEnd('/'));
        foreach (var limited in limitedPaths)
        {
            if (path.Equals(limited, StringComparison.OrdinalIgnoreCase))
            {
                return limited;
            }
        }

        return null;
    }
}
