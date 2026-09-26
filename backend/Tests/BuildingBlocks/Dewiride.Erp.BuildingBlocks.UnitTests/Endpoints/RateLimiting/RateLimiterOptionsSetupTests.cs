using System.Net;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.RateLimiting;

public sealed class RateLimiterOptionsSetupTests
{
    private static readonly Guid Alice = Guid.Parse("7d1c3f0e-6a55-4c1b-9d1e-2f7a9b3c4d5e");

    [Fact]
    public void Configure_Enabled_SetsTheGlobalLimiterAndTheTooManyRequestsStatus()
    {
        var limiter = Configure(new RateLimitingOptions());

        Assert.NotNull(limiter.GlobalLimiter);
        Assert.Equal(StatusCodes.Status429TooManyRequests, limiter.RejectionStatusCode);
        Assert.NotNull(limiter.OnRejected);
    }

    [Fact]
    public void Configure_Disabled_SetsNoGlobalLimiter()
    {
        Assert.Null(Configure(new RateLimitingOptions { Enabled = false }).GlobalLimiter);
    }

    [Fact]
    public async Task OnRejected_SignedInCallerOverTheLimit_AnswersRetryAfterOfOneActorSegment()
    {
        var limiter = Configure(new RateLimitingOptions { ActorPermitLimit = 1, ActorWindow = TimeSpan.FromMinutes(1), ActorSegmentsPerWindow = 6 });
        var context = Context(new FixedActorContext(Alice, IsAuthenticated: true));

        var retryAfter = await RejectAsync(limiter, context);

        Assert.Equal("10", retryAfter);
        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<RecordingProblemDetailsService>(context.RequestServices.GetRequiredService<IProblemDetailsService>()).Status);
    }

    [Fact]
    public async Task OnRejected_AnonymousCallerOverTheLimit_AnswersTheWaitTheLeaseReports()
    {
        var limiter = Configure(new RateLimitingOptions { AnonymousPermitLimit = 1, AnonymousWindow = TimeSpan.FromMinutes(2) });

        var retryAfter = await RejectAsync(limiter, Context(new FixedActorContext(ActorIds.Anonymous, IsAuthenticated: false)));

        Assert.Equal("120", retryAfter);
    }

    private static RateLimiterOptions Configure(RateLimitingOptions options)
    {
        var limiter = new RateLimiterOptions();
        new RateLimiterOptionsSetup(Microsoft.Extensions.Options.Options.Create(options)).Configure(limiter);

        return limiter;
    }

    private static async Task<string?> RejectAsync(RateLimiterOptions limiter, HttpContext context)
    {
        using var global = limiter.GlobalLimiter!;
        using var first = global.AttemptAcquire(context);
        using var rejected = global.AttemptAcquire(context);
        Assert.True(first.IsAcquired);
        Assert.False(rejected.IsAcquired);

        await limiter.OnRejected!(new OnRejectedContext { HttpContext = context, Lease = rejected }, TestContext.Current.CancellationToken);

        return context.Response.Headers.RetryAfter;
    }

    private static DefaultHttpContext Context(IActorContext actor)
    {
        var services = new ServiceCollection().AddSingleton(actor).AddSingleton<IProblemDetailsService, RecordingProblemDetailsService>();
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        return context;
    }

    private sealed record FixedActorContext(Guid ActorId, bool IsAuthenticated) : IActorContext;

    private sealed class RecordingProblemDetailsService : IProblemDetailsService
    {
        public int? Status { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Status = context.ProblemDetails.Status;
            return ValueTask.CompletedTask;
        }
    }
}
