using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence;

public sealed class BulkWriteGuardTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    [Fact]
    public async Task ExecuteDeleteAsync_SoftDeletableAggregate_ThrowsInsteadOfDroppingRows()
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Samples.Where(s => s.Name == "nobody").ExecuteDeleteAsync(TestContext.Current.CancellationToken));

        Assert.Contains(nameof(SampleAggregate), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteUpdateAsync_AuditableAggregate_ThrowsInsteadOfSkippingTheStamps()
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Samples.Where(s => s.Name == "nobody").ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "renamed"), TestContext.Current.CancellationToken));

        Assert.Contains(nameof(SampleAggregate), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteDeleteAsync_PlainChildEntity_StillRuns()
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var affected = await context.Set<SampleLine>().Where(l => l.Description == "nobody").ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, affected);
    }
}
