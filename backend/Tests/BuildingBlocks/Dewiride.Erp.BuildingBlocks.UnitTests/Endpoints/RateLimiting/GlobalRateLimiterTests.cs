using System.Net;
using System.Threading.RateLimiting;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.RateLimiting;

public sealed class GlobalRateLimiterTests
{
    private static readonly RateLimitingOptions Options = new()
    {
        AnonymousPermitLimit = 2,
        AnonymousWindow = TimeSpan.FromMinutes(1),
        ActorPermitLimit = 3,
        ActorWindow = TimeSpan.FromMinutes(1),
        ActorSegmentsPerWindow = 6,
    };

    private static readonly Guid Alice = Guid.Parse("7d1c3f0e-6a55-4c1b-9d1e-2f7a9b3c4d5e");

    private static readonly Guid Bob = Guid.Parse("0b9e8d7c-6b5a-4f3e-8d2c-1b0a9f8e7d6c");

    [Fact]
    public void AttemptAcquire_AnonymousCallerOverItsLimit_IsRejectedWhileAnotherAddressIsServed()
    {
        using var limiter = GlobalRateLimiter.Create(Options);

        Assert.True(Acquire(limiter, Anonymous("203.0.113.7")));
        Assert.True(Acquire(limiter, Anonymous("203.0.113.7")));
        Assert.False(Acquire(limiter, Anonymous("203.0.113.7")));
        Assert.True(Acquire(limiter, Anonymous("203.0.113.8")));
    }

    [Fact]
    public void AttemptAcquire_SignedInActorAcrossAddresses_SharesOneAllowance()
    {
        using var limiter = GlobalRateLimiter.Create(Options);

        Assert.True(Acquire(limiter, SignedIn(Alice, "203.0.113.7")));
        Assert.True(Acquire(limiter, SignedIn(Alice, "198.51.100.1")));
        Assert.True(Acquire(limiter, SignedIn(Alice, "2001:db8::1")));
        Assert.False(Acquire(limiter, SignedIn(Alice, "203.0.113.9")));
    }

    [Fact]
    public void AttemptAcquire_SignedInActorsBehindOneAddress_EachHaveTheirOwnAllowance()
    {
        using var limiter = GlobalRateLimiter.Create(Options);

        for (var i = 0; i < Options.ActorPermitLimit; i++)
        {
            Assert.True(Acquire(limiter, SignedIn(Alice, "203.0.113.7")));
        }

        Assert.False(Acquire(limiter, SignedIn(Alice, "203.0.113.7")));
        Assert.True(Acquire(limiter, SignedIn(Bob, "203.0.113.7")));
        Assert.True(Acquire(limiter, Anonymous("203.0.113.7")));
    }

    [Fact]
    public void AttemptAcquire_Rejected_CarriesARetryAfterEstimate()
    {
        using var limiter = GlobalRateLimiter.Create(Options);
        Acquire(limiter, Anonymous("203.0.113.7"));
        Acquire(limiter, Anonymous("203.0.113.7"));

        using var lease = limiter.AttemptAcquire(Anonymous("203.0.113.7"));

        Assert.False(lease.IsAcquired);
        Assert.True(lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
        Assert.InRange(retryAfter, TimeSpan.FromTicks(1), Options.AnonymousWindow);
    }

    private static bool Acquire(PartitionedRateLimiter<HttpContext> limiter, HttpContext context)
    {
        using var lease = limiter.AttemptAcquire(context);

        return lease.IsAcquired;
    }

    private static DefaultHttpContext Anonymous(string address) => Context(new FixedActorContext(ActorIds.Anonymous, IsAuthenticated: false), address);

    private static DefaultHttpContext SignedIn(Guid actorId, string address) => Context(new FixedActorContext(actorId, IsAuthenticated: true), address);

    private static DefaultHttpContext Context(IActorContext actor, string address)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(actor).BuildServiceProvider() };
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        return context;
    }

    private sealed record FixedActorContext(Guid ActorId, bool IsAuthenticated) : IActorContext;
}
