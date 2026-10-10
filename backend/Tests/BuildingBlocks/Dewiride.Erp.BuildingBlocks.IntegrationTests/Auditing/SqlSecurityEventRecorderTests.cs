using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Auditing;

public sealed class SqlSecurityEventRecorderTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    [Fact]
    public async Task RecordAsync_Entry_WritesOneRowAtTheClocksTime()
    {
        var correlationId = Guid.CreateVersion7().ToString("N");
        var actor = Guid.CreateVersion7();
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        await using (var scope = database.CreateScope())
        {
            var recorder = new SqlSecurityEventRecorder(scope.ServiceProvider.GetRequiredService<AuditingDbContext>(), clock);
            await recorder.RecordAsync(
                new SecurityEventEntry(SecurityEventKind.SignedIn, ActorObjectId: actor, ClientAddress: "198.51.100.23", CorrelationId: correlationId),
                TestContext.Current.CancellationToken);
        }

        await using var check = database.CreateScope();
        var recorded = await check.ServiceProvider.GetRequiredService<AuditingDbContext>().SecurityEvents
            .AsNoTracking()
            .SingleAsync(e => e.CorrelationId == correlationId, TestContext.Current.CancellationToken);
        Assert.Equal(SecurityEventKind.SignedIn, recorded.Kind);
        Assert.Equal(actor, recorded.ActorObjectId);
        Assert.Null(recorded.ClientApplicationId);
        Assert.Null(recorded.Detail);
        Assert.Equal("198.51.100.23", recorded.ClientAddress);
        Assert.Equal(clock.GetUtcNow(), recorded.OccurredAt);
    }
}
