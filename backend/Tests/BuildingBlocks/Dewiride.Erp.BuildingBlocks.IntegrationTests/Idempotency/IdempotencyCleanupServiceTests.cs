using System.Security.Cryptography;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Idempotency;
using Dewiride.Erp.BuildingBlocks.Idempotency.Cleanup;
using Dewiride.Erp.BuildingBlocks.Idempotency.Persistence;
using Dewiride.Erp.BuildingBlocks.Idempotency.Storage;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Idempotency;

public sealed class IdempotencyCleanupServiceTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private static readonly Guid Actor = new("0199a1b2-0000-7000-8000-0000000000bb");

    [Fact]
    public async Task CleanupOnceAsync_ExpiredAndLiveRows_RemovesTheExpiredOnesAndLogsTheCount()
    {
        var expired = Guid.CreateVersion7();
        var live = Guid.CreateVersion7();
        var now = database.Clock.GetUtcNow();
        await using (var scope = database.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            await store.BeginAsync(Actor, expired, SHA256.HashData(Encoding.UTF8.GetBytes("a")), now.AddHours(-3), now.AddHours(-2), TestContext.Current.CancellationToken);
            await store.BeginAsync(Actor, live, SHA256.HashData(Encoding.UTF8.GetBytes("b")), now, now.AddHours(1), TestContext.Current.CancellationToken);
        }

        var logger = new FakeLogger<IdempotencyCleanupService>();
        using var service = new IdempotencyCleanupService(database.ScopeFactory, database.Clock, Options.Create(new IdempotencyOptions()), logger);
        await service.CleanupOnceAsync(TestContext.Current.CancellationToken);

        await using var check = database.CreateScope();
        var records = check.ServiceProvider.GetRequiredService<IdempotencyDbContext>().Records;
        Assert.False(await records.AnyAsync(r => r.Key == expired, TestContext.Current.CancellationToken));
        Assert.True(await records.AnyAsync(r => r.Key == live, TestContext.Current.CancellationToken));
        Assert.StartsWith("Removed ", Assert.Single(logger.Collector.GetSnapshot()).Message, StringComparison.Ordinal);
    }
}
