using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence;

public sealed class AuditingTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private static readonly Guid Alice = new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid Bob = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task SaveChangesAsync_AddedAggregate_StampsCreatedFromTheActorAndTheClock()
    {
        var id = SampleId.Create();
        var createdAt = database.Clock.GetUtcNow();
        database.Actor.ActorId = Alice;

        await database.AddAsync(new SampleAggregate(id, "Created", new Money(10m, Currency.Inr), new SampleAddress("1", "Jaipur"), createdAt));

        var sample = await database.FindAsync(id);
        Assert.Equal(createdAt, sample.CreatedAt);
        Assert.Equal(Alice, sample.CreatedBy);
        Assert.Null(sample.ModifiedAt);
        Assert.Null(sample.ModifiedBy);
        Assert.False(sample.IsDeleted);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedAggregate_StampsModifiedOnlyAndKeepsCreated()
    {
        var id = SampleId.Create();
        database.Actor.ActorId = Alice;
        await database.AddAsync(new SampleAggregate(id, "Original", new Money(10m, Currency.Inr), new SampleAddress("1", "Jaipur"), database.Clock.GetUtcNow()));
        var createdAt = (await database.FindAsync(id)).CreatedAt;
        database.Clock.Advance(TimeSpan.FromMinutes(5));
        database.Actor.ActorId = Bob;

        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            var tracked = await context.Samples.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
            tracked.Rename("Renamed", database.Clock.GetUtcNow());
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sample = await database.FindAsync(id);
        Assert.Equal("Renamed", sample.Name);
        Assert.Equal(createdAt, sample.CreatedAt);
        Assert.Equal(Alice, sample.CreatedBy);
        Assert.Equal(database.Clock.GetUtcNow(), sample.ModifiedAt);
        Assert.Equal(Bob, sample.ModifiedBy);
    }

    [Fact]
    public async Task SaveChangesAsync_HostWithoutHttp_StampsTheSystemActor()
    {
        var id = SampleId.Create();
        database.Actor.ActorId = ActorIds.System;

        await database.AddAsync(new SampleAggregate(id, "System", new Money(1m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));

        Assert.Equal(ActorIds.System, (await database.FindAsync(id)).CreatedBy);
    }

    [Fact]
    public async Task SaveChanges_Synchronous_ThrowsSoEveryWriteStaysAsynchronous()
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        context.Samples.Add(new SampleAggregate(SampleId.Create(), "Sync", new Money(1m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));

        var exception = Assert.Throws<NotSupportedException>(() => context.SaveChanges());

        Assert.Contains("SaveChangesAsync", exception.Message, StringComparison.Ordinal);
    }
}
