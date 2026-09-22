using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence;

public sealed class SoftDeleteTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private static readonly Guid Carol = new("33333333-3333-3333-3333-333333333333");

    private static readonly Guid Dave = new("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Remove_SoftDeletableAggregate_KeepsTheRowStampedAndHiddenFromQueries()
    {
        var id = SampleId.Create();
        await database.AddAsync(new SampleAggregate(id, "Doomed", new Money(5m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));
        database.Clock.Advance(TimeSpan.FromMinutes(1));
        database.Actor.ActorId = Carol;

        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            context.Samples.Remove(await context.Samples.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            Assert.False(await context.Samples.AnyAsync(s => s.Id == id, TestContext.Current.CancellationToken));
            var rows = await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [test_sample].[Samples] WHERE [Id] = {0}", id.Value).SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, rows);
        }

        var deleted = await database.FindAsync(id, includeDeleted: true);
        Assert.True(deleted.IsDeleted);
        Assert.Equal(database.Clock.GetUtcNow(), deleted.DeletedAt);
        Assert.Equal(Carol, deleted.DeletedBy);
        Assert.Equal(database.Clock.GetUtcNow(), deleted.ModifiedAt);
        Assert.Equal(Carol, deleted.ModifiedBy);
    }

    [Fact]
    public async Task Remove_AlreadyDeletedAggregate_KeepsTheOriginalDeleteStamps()
    {
        var id = SampleId.Create();
        await database.AddAsync(new SampleAggregate(id, "Twice", new Money(5m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));
        database.Actor.ActorId = Carol;
        await RemoveAsync(id, includeDeleted: false);
        var first = await database.FindAsync(id, includeDeleted: true);
        database.Clock.Advance(TimeSpan.FromHours(1));
        database.Actor.ActorId = Dave;

        await RemoveAsync(id, includeDeleted: true);

        var second = await database.FindAsync(id, includeDeleted: true);
        Assert.True(second.IsDeleted);
        Assert.Equal(first.DeletedAt, second.DeletedAt);
        Assert.Equal(Carol, second.DeletedBy);
        Assert.Equal(first.ModifiedAt, second.ModifiedAt);
        Assert.Equal(first.RowVersion, second.RowVersion);
    }

    [Fact]
    public async Task Remove_SoftDeletableRootWithLoadedLines_KeepsTheLineRows()
    {
        var id = SampleId.Create();
        var sample = new SampleAggregate(id, "Parent", new Money(5m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow());
        sample.AddLine("first");
        sample.AddLine("second");
        await database.AddAsync(sample);

        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            var tracked = await context.Samples.Include(s => s.Lines).SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
            context.Samples.Remove(tracked);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.All(tracked.Lines, line => Assert.Equal(EntityState.Unchanged, context.Entry(line).State));
        }

        await using var query = database.CreateScope();
        var lines = query.ServiceProvider.GetRequiredService<SampleDbContext>().Samples
            .IncludeDeleted()
            .Where(s => s.Id == id)
            .SelectMany(s => s.Lines)
            .Select(l => l.Description);
        Assert.True((await database.FindAsync(id, includeDeleted: true)).IsDeleted);
        Assert.Equal(["first", "second"], (await lines.ToListAsync(TestContext.Current.CancellationToken)).Order());
    }

    [Fact]
    public async Task IncludeDeleted_IgnoresOnlyTheSoftDeleteFilter()
    {
        var live = SampleId.Create();
        var gone = SampleId.Create();
        await database.AddAsync(new SampleAggregate(live, "Live", new Money(1m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));
        await database.AddAsync(new SampleAggregate(gone, "Gone", new Money(1m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));
        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            context.Samples.Remove(await context.Samples.SingleAsync(s => s.Id == gone, TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var query = database.CreateScope();
        var samples = query.ServiceProvider.GetRequiredService<SampleDbContext>().Samples;
        SampleId[] ids = [live, gone];
        var visible = await samples.Where(s => ids.Contains(s.Id)).Select(s => s.Id).ToListAsync(TestContext.Current.CancellationToken);
        var all = await samples.IncludeDeleted().Where(s => ids.Contains(s.Id)).Select(s => s.Id).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([live], visible);
        Assert.Equal([live, gone], all.OrderBy(id => id == gone).ToList());
        Assert.Equal(SoftDeleteFilter.Name, Assert.Single(samples.EntityType.GetDeclaredQueryFilters()).Key);
    }
    private async Task RemoveAsync(SampleId id, bool includeDeleted)
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var query = includeDeleted ? context.Samples.IncludeDeleted() : context.Samples;
        context.Samples.Remove(await query.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
