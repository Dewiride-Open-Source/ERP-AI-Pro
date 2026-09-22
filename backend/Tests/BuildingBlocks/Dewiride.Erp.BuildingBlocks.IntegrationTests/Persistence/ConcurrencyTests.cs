using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence;

public sealed class ConcurrencyTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    [Fact]
    public async Task SaveChangesAsync_StaleRowVersion_ThrowsDbUpdateConcurrencyException()
    {
        var id = SampleId.Create();
        await database.AddAsync(new SampleAggregate(id, "Contended", new Money(1m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));

        await using var first = database.CreateScope();
        await using var second = database.CreateScope();
        var firstContext = first.ServiceProvider.GetRequiredService<SampleDbContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<SampleDbContext>();
        var firstCopy = await firstContext.Samples.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        var secondCopy = await secondContext.Samples.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        var versionBefore = firstCopy.RowVersion.ToArray();

        firstCopy.Rename("First writer");
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        secondCopy.Rename("Second writer");

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("First writer", (await database.FindAsync(id)).Name);
        Assert.NotEqual(versionBefore, (await database.FindAsync(id)).RowVersion);
    }

    [Fact]
    public async Task Model_RowVersionProperty_IsTheConcurrencyToken()
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var property = context.Model.FindEntityType(typeof(SampleAggregate))!.FindProperty(nameof(SampleAggregate.RowVersion))!;

        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
        Assert.Equal("rowversion", property.GetColumnType());
    }
}
