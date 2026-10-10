using Dewiride.Erp.BuildingBlocks.Auditing;
using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Auditing;

public sealed class SecurityEventRetentionSweeperTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    [Fact]
    public async Task SweepOnceAsync_EventsOnEitherSideOfTheRetention_DeletesOnlyTheOlderAndLogsTheCount()
    {
        var older = Guid.CreateVersion7().ToString("N");
        var newer = Guid.CreateVersion7().ToString("N");

        // A month ahead of the real clock, so no sweep of another host, which runs on the real clock, reaches the older event.
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow().AddDays(30));
        await AddAsync(older, clock.GetUtcNow().AddDays(-366));
        await AddAsync(newer, clock.GetUtcNow().AddDays(-364));

        var logger = new FakeLogger<SecurityEventRetentionSweeper>();
        using var sweeper = new SecurityEventRetentionSweeper(database.ScopeFactory, clock, Options.Create(new AuditingOptions()), logger);
        await sweeper.SweepOnceAsync(TestContext.Current.CancellationToken);

        await using var check = database.CreateScope();
        var events = check.ServiceProvider.GetRequiredService<AuditingDbContext>().SecurityEvents;
        Assert.False(await events.AnyAsync(e => e.CorrelationId == older, TestContext.Current.CancellationToken));
        Assert.True(await events.AnyAsync(e => e.CorrelationId == newer, TestContext.Current.CancellationToken));
        Assert.Equal("1", Assert.Single(logger.Collector.GetSnapshot()).GetStructuredStateValue("Count"));
    }

    private async Task AddAsync(string correlationId, DateTimeOffset occurredAt)
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditingDbContext>();
        context.SecurityEvents.Add(SecurityEvent.Record(new SecurityEventEntry(SecurityEventKind.SignInFailed, "protocol:none", CorrelationId: correlationId), occurredAt));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
