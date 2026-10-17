using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Notes;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
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

    [Fact]
    public async Task ExecuteDeleteAsync_AuditableTypeThatIsNotSoftDeletable_ThrowsInsteadOfDeletingWithoutARecord()
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Notes.Where(n => n.Text == "nobody").ExecuteDeleteAsync(TestContext.Current.CancellationToken));

        Assert.Contains(nameof(SampleNote), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteUpdateAsync_AppendOnlyTypes_ThrowInsteadOfRewritingTheTrail()
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditingDbContext>();

        var records = await Assert.ThrowsAsync<InvalidOperationException>(() => context.ChangeRecords.Where(r => r.EntityId == Guid.Empty).ExecuteUpdateAsync(r => r.SetProperty(x => x.ValueCount, (short)1), TestContext.Current.CancellationToken));
        var events = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SecurityEvents.Where(e => e.Detail == "nobody").ExecuteUpdateAsync(e => e.SetProperty(x => x.Detail, "rewritten"), TestContext.Current.CancellationToken));

        Assert.Contains(nameof(ChangeRecord), records.Message, StringComparison.Ordinal);
        Assert.Contains("append-only", events.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteDeleteAsync_AppendOnlyType_StillRunsForTheRetentionOfItsTable()
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditingDbContext>();
        var nobody = Guid.CreateVersion7();

        var affected = await context.ChangeRecords.Where(r => r.EntityId == nobody).ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, affected);
    }
}
