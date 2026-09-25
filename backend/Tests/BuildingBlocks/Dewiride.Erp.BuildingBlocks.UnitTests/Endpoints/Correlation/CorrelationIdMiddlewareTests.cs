using System.Diagnostics;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Correlation;

public sealed class CorrelationIdMiddlewareTests : IDisposable
{
    private readonly ActivitySource _source = new("Dewiride.Erp.Tests.Correlation");

    private readonly ActivityListener _listener;

    public CorrelationIdMiddlewareTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = candidate => ReferenceEquals(candidate, _source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public async Task InvokeAsync_WellFormedSuppliedHeader_KeepsTheCallersValue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationId.HeaderName] = "order-4711";

        var correlationId = await ObserveAsync(context);

        Assert.Equal("order-4711", correlationId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public async Task InvokeAsync_MalformedSuppliedHeader_ReplacesItWithTheTraceId(string supplied)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationId.HeaderName] = supplied;
        using var activity = _source.StartActivity("request");

        var correlationId = await ObserveAsync(context);

        Assert.Equal(activity!.TraceId.ToHexString(), correlationId);
    }

    [Fact]
    public async Task InvokeAsync_TwoSuppliedHeaderValues_ReplacesThemWithTheTraceId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationId.HeaderName] = new[] { "first", "second" };
        using var activity = _source.StartActivity("request");

        var correlationId = await ObserveAsync(context);

        Assert.Equal(activity!.TraceId.ToHexString(), correlationId);
    }

    [Fact]
    public async Task InvokeAsync_NoSuppliedHeaderAndNoActivity_UsesAWellFormedGeneratedValue()
    {
        var correlationId = await ObserveAsync(new DefaultHttpContext());

        Assert.NotNull(correlationId);
        Assert.True(CorrelationId.IsWellFormed(correlationId));
        Assert.Equal(32, correlationId.Length);
    }

    [Fact]
    public async Task InvokeAsync_Always_TagsTheCurrentActivityWithTheCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationId.HeaderName] = "order-4711";
        using var activity = _source.StartActivity("request");

        await ObserveAsync(context);

        Assert.Equal("order-4711", activity!.GetTagItem(CorrelationIdMiddleware.ActivityTagName));
    }

    [Fact]
    public async Task InvokeAsync_WhileTheRestOfThePipelineRuns_KeepsTheCorrelationIdInTheLogScope()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationId.HeaderName] = "order-4711";
        var logger = new FakeLogger<CorrelationIdMiddleware>();
        var middleware = new CorrelationIdMiddleware(_ => Write(logger, "downstream"), logger);

        await middleware.InvokeAsync(context);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        var scope = Assert.IsType<CorrelationIdScope>(Assert.Single(record.Scopes));
        Assert.Equal(new KeyValuePair<string, object>(CorrelationIdScope.PropertyName, "order-4711"), scope[0]);
        Assert.Equal($"{CorrelationIdScope.PropertyName}:order-4711", scope.ToString());
    }

    [Fact]
    public async Task InvokeAsync_AfterThePipelineRan_ClosedTheLogScope()
    {
        var logger = new FakeLogger<CorrelationIdMiddleware>();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, logger);

        await middleware.InvokeAsync(new DefaultHttpContext());
        await Write(logger, "after");

        Assert.Empty(Assert.Single(logger.Collector.GetSnapshot()).Scopes);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    private static Task Write(FakeLogger<CorrelationIdMiddleware> logger, string message)
    {
        logger.Log(LogLevel.Information, default, message, null, static (state, _) => state);

        return Task.CompletedTask;
    }

    private static async Task<string?> ObserveAsync(HttpContext context)
    {
        string? observed = null;
        var middleware = new CorrelationIdMiddleware(
            invoked =>
            {
                observed = invoked.Features.Get<ICorrelationIdFeature>()?.CorrelationId;
                return Task.CompletedTask;
            },
            new FakeLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        return observed;
    }
}
