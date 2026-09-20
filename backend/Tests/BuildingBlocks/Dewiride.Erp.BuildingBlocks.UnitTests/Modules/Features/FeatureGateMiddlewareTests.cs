using Dewiride.Erp.BuildingBlocks.Modules.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Modules.Features;

public sealed class FeatureGateMiddlewareTests
{
    private const string Flag = "Erp.Modules.Finance.Sales";

    [Fact]
    public async Task InvokeAsync_EndpointWithoutGate_CallsNext()
    {
        var (context, problems) = Context(gated: false);
        var nextCalled = false;
        var middleware = new FeatureGateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new FixedFeatureManager(enabled: false), problems);

        Assert.True(nextCalled);
        Assert.Null(problems.Written);
    }

    [Fact]
    public async Task InvokeAsync_GatedEndpointWithTheFeatureEnabled_CallsNext()
    {
        var (context, problems) = Context(gated: true);
        var nextCalled = false;
        var middleware = new FeatureGateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new FixedFeatureManager(enabled: true), problems);

        Assert.True(nextCalled);
        Assert.Null(problems.Written);
    }

    [Fact]
    public async Task InvokeAsync_GatedEndpointWithTheFeatureDisabled_WritesNotFoundProblemWithoutCallingNext()
    {
        var (context, problems) = Context(gated: true);
        var nextCalled = false;
        var middleware = new FeatureGateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new FixedFeatureManager(enabled: false), problems);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problems.Written);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("/api/finance/sales", problem.Instance);
        Assert.Contains(Flag, problem.Detail, StringComparison.Ordinal);
        Assert.Equal(FeatureGateMiddleware.DisabledCode, problem.Extensions["code"]);
    }

    private static (DefaultHttpContext Context, RecordingProblemDetailsService Problems) Context(bool gated)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Request.Path = "/api/finance/sales";
        var metadata = gated ? new EndpointMetadataCollection(new RequireFeatureMetadata(Flag)) : EndpointMetadataCollection.Empty;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));

        return (context, new RecordingProblemDetailsService());
    }

    private sealed class RecordingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetails? Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedFeatureManager(bool enabled) : IVariantFeatureManagerSnapshot
    {
        public IAsyncEnumerable<string> GetFeatureNamesAsync(CancellationToken cancellationToken = default) => AsyncEnumerable.Empty<string>();

        public ValueTask<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken = default) => ValueTask.FromResult(enabled);

        public ValueTask<bool> IsEnabledAsync<TContext>(string feature, TContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(enabled);

        public ValueTask<Variant> GetVariantAsync(string feature, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<Variant> GetVariantAsync(string feature, ITargetingContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
