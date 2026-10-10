using System.Net;
using System.Threading.RateLimiting;
using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.RateLimiting;

public sealed class SignInRateLimiterTests
{
    private const string Login = "/api/auth/login";

    private const string Callback = "/api/auth/signin-oidc";

    private const string Logout = "/api/auth/logout";

    private static readonly RateLimitingOptions Options = new()
    {
        SignInPermitLimit = 2,
        SignInWindow = TimeSpan.FromMinutes(1),
    };

    private static readonly string[] Paths = [Login, Callback, Logout];

    [Fact]
    public void AttemptAcquire_AddressOverTheAllowanceOfAPath_IsRejectedWhileAnotherAddressIsServed()
    {
        using var limiter = SignInRateLimiter.Create(Options, Paths);

        Assert.True(Acquire(limiter, Request(Login, "203.0.113.7")));
        Assert.True(Acquire(limiter, Request(Login, "203.0.113.7")));
        Assert.False(Acquire(limiter, Request(Login, "203.0.113.7")));
        Assert.True(Acquire(limiter, Request(Login, "203.0.113.8")));
    }

    [Fact]
    public void AttemptAcquire_EachSignInPathOfOneAddress_HasAnAllowanceOfItsOwn()
    {
        using var limiter = SignInRateLimiter.Create(Options, Paths);
        Exhaust(limiter, Login, "203.0.113.7");

        Assert.True(Acquire(limiter, Request(Callback, "203.0.113.7")));
        Assert.True(Acquire(limiter, Request(Logout, "203.0.113.7")));
        Assert.True(Acquire(limiter, Request(Logout, "203.0.113.7")));
        Assert.False(Acquire(limiter, Request(Logout, "203.0.113.7")));
        Assert.False(Acquire(limiter, Request(Login, "203.0.113.7")));
    }

    [Theory]
    [InlineData("/API/AUTH/LOGIN", Login)]
    [InlineData("/api/auth/login/", Login)]
    [InlineData("/api/auth/login//", Login)]
    [InlineData("/Api/Auth/SignIn-Oidc/", Callback)]
    public void AttemptAcquire_SignInPathAsRoutingMatchesIt_CountsAgainstThatPathsAllowance(string path, string limitedPath)
    {
        using var limiter = SignInRateLimiter.Create(Options, Paths);
        Exhaust(limiter, limitedPath, "203.0.113.7");

        Assert.False(Acquire(limiter, Request(path, "203.0.113.7")));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/api/auth/me")]
    [InlineData("/api/auth/login/again")]
    [InlineData("/api//auth/login")]
    [InlineData("/api/auth")]
    public void AttemptAcquire_OtherPath_IsNeverLimited(string path)
    {
        using var limiter = SignInRateLimiter.Create(Options, Paths);

        for (var i = 0; i <= Options.SignInPermitLimit * 2; i++)
        {
            Assert.True(Acquire(limiter, Request(path, "203.0.113.7")));
        }
    }

    [Fact]
    public void AttemptAcquire_Rejected_CarriesARetryAfterWithinTheWindow()
    {
        using var limiter = SignInRateLimiter.Create(Options, Paths);
        Exhaust(limiter, Login, "203.0.113.7");

        using var lease = limiter.AttemptAcquire(Request(Login, "203.0.113.7"));

        Assert.False(lease.IsAcquired);
        Assert.True(lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
        Assert.InRange(retryAfter, TimeSpan.FromTicks(1), Options.SignInWindow);
    }

    private static void Exhaust(PartitionedRateLimiter<HttpContext> limiter, string path, string address)
    {
        for (var i = 0; i < Options.SignInPermitLimit; i++)
        {
            Assert.True(Acquire(limiter, Request(path, address)));
        }
    }

    private static bool Acquire(PartitionedRateLimiter<HttpContext> limiter, HttpContext context)
    {
        using var lease = limiter.AttemptAcquire(context);

        return lease.IsAcquired;
    }

    private static DefaultHttpContext Request(string path, string address)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        return context;
    }
}
