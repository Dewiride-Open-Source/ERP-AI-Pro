using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Idempotency;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Idempotency;

public sealed class IdempotentEndpointsFixture : IAsyncDisposable
{
    public const string OrdersPath = "/__test/orders";

    public const string SlowPath = "/__test/slow-orders";

    public const string FailingPath = "/__test/failing-orders";

    public const string LargePath = "/__test/large-orders";

    public const int MaxStoredResponseBytes = 1024;

    private int _orders;

    public IdempotentEndpointsFixture()
    {
        Factory = new ErpApiFactory()
            .WithConfiguration($"{IdempotencyOptions.SectionName}:MaxStoredResponseBytes", MaxStoredResponseBytes.ToString(CultureInfo.InvariantCulture))
            .WithTestEndpoints(routes =>
        {
            routes.MapPost(OrdersPath, (OrderRequest order) =>
            {
                var number = Interlocked.Increment(ref _orders);
                return Results.Created($"{OrdersPath}/{number}", new OrderResponse(number, order.Item));
            }).RequireIdempotencyKey();
            routes.MapPost(SlowPath, async (OrderRequest order, CancellationToken cancellationToken) =>
            {
                Entered.Release();
                await Gate.WaitAsync(cancellationToken);
                return Results.Ok(new OrderResponse(Interlocked.Increment(ref _orders), order.Item));
            }).RequireIdempotencyKey();
            routes.MapPost(FailingPath, (OrderRequest order) => order.Item == "explode"
                ? throw new InvalidOperationException("boom")
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable)).RequireIdempotencyKey();
            routes.MapPost(LargePath, (OrderRequest order) => Results.Ok(new OrderResponse(Interlocked.Increment(ref _orders), new string('x', MaxStoredResponseBytes * 2)))).RequireIdempotencyKey();
        });
    }

    public ErpApiFactory Factory { get; }

    public SemaphoreSlim Entered { get; } = new(0);

    public SemaphoreSlim Gate { get; } = new(0);

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        Entered.Dispose();
        Gate.Dispose();
    }

    public sealed record OrderRequest(string Item);

    public sealed record OrderResponse(int Number, string Item);
}
