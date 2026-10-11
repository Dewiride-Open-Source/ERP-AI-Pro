using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence;

public sealed class TransactionLocksTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    [Fact]
    public async Task TryAcquireExclusiveAsync_ResourceAnotherTransactionHolds_RefusesItUntilThatTransactionCommits()
    {
        var resource = UniqueResource();
        await using var first = database.CreateScope();
        await using var second = database.CreateScope();
        var holder = first.ServiceProvider.GetRequiredService<SampleDbContext>();
        var contender = second.ServiceProvider.GetRequiredService<SampleDbContext>();
        await using var held = await holder.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var waiting = await contender.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        Assert.True(await TransactionLocks.TryAcquireExclusiveAsync(holder, resource, TimeSpan.Zero, TestContext.Current.CancellationToken));
        Assert.False(await TransactionLocks.TryAcquireExclusiveAsync(contender, resource, TimeSpan.Zero, TestContext.Current.CancellationToken));

        await held.CommitAsync(TestContext.Current.CancellationToken);

        Assert.True(await TransactionLocks.TryAcquireExclusiveAsync(contender, resource, TimeSpan.Zero, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireExclusiveAsync_ResourceOfARolledBackTransaction_GrantsItToTheNext()
    {
        var resource = UniqueResource();
        await using var first = database.CreateScope();
        await using var second = database.CreateScope();
        var holder = first.ServiceProvider.GetRequiredService<SampleDbContext>();
        var contender = second.ServiceProvider.GetRequiredService<SampleDbContext>();
        await using var held = await holder.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var waiting = await contender.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        Assert.True(await TransactionLocks.TryAcquireExclusiveAsync(holder, resource, TimeSpan.Zero, TestContext.Current.CancellationToken));

        await held.RollbackAsync(TestContext.Current.CancellationToken);

        Assert.True(await TransactionLocks.TryAcquireExclusiveAsync(contender, resource, TimeSpan.Zero, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireExclusiveAsync_DifferentResources_GrantsBoth()
    {
        await using var first = database.CreateScope();
        await using var second = database.CreateScope();
        var one = first.ServiceProvider.GetRequiredService<SampleDbContext>();
        var other = second.ServiceProvider.GetRequiredService<SampleDbContext>();
        await using var oneTransaction = await one.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var otherTransaction = await other.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        Assert.True(await TransactionLocks.TryAcquireExclusiveAsync(one, UniqueResource(), TimeSpan.Zero, TestContext.Current.CancellationToken));
        Assert.True(await TransactionLocks.TryAcquireExclusiveAsync(other, UniqueResource(), TimeSpan.Zero, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireExclusiveAsync_WithoutATransaction_Throws()
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => TransactionLocks.TryAcquireExclusiveAsync(context, UniqueResource(), TimeSpan.Zero, TestContext.Current.CancellationToken));
    }

    private static string UniqueResource() => $"erp-test-lock-{Guid.CreateVersion7():N}";
}
