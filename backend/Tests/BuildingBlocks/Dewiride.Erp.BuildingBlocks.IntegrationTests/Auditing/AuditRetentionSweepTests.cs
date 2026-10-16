using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Auditing;
using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.Auditing.Retention;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Auditing;

// A sweep deletes every row past its retention in the per-process test database, not only its own test's. Every row here is
// dated before 2006 and every sweep's clock is set before 2012, while the other test classes write their audit rows at the real
// time or the sample clock's, both in 2026, so no sweep here reaches another class's row; the tests of this class run one after
// another, so no two of its sweeps race either. A case of the boundary theory gives the time of a change in UTC where its IST
// date differs from its UTC one.
public sealed class AuditRetentionSweepTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private const string ChangeKey = "test.audit.retention";

    private const int RetentionYears = 8;

    [Fact]
    public async Task SweepAsync_SecurityEventsOnEitherSideOfTheRetention_DeletesOnlyThoseRecordedBeforeIt()
    {
        var now = new DateTimeOffset(2005, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var cutoff = now - new AuditingOptions().SecurityEventRetention;
        var older = await AddSecurityEventAsync(cutoff.AddSeconds(-1));
        var atTheCutoff = await AddSecurityEventAsync(cutoff);
        var newer = await AddSecurityEventAsync(cutoff.AddSeconds(1));
        var due = await CountSecurityEventsBeforeAsync(cutoff);

        var logger = new FakeLogger<AuditRetentionSweep>();
        await SweepAsync(now, changeRecordRetentionYears: null, logger);

        Assert.False(await SecurityEventExistsAsync(older));
        Assert.True(await SecurityEventExistsAsync(atTheCutoff));
        Assert.True(await SecurityEventExistsAsync(newer));
        Assert.Equal(due.ToString(CultureInfo.InvariantCulture), DeletedCount(logger, "security events"));
    }

    [Theory]
    [InlineData("2010-06-15T12:00:00+05:30", "2002-03-31T23:59:59+05:30", true)]
    [InlineData("2010-06-15T12:00:00+05:30", "2002-04-01T00:00:00+05:30", false)]
    [InlineData("2010-06-15T12:00:00+05:30", "2002-03-31T20:00:00+00:00", false)]
    [InlineData("2011-03-31T23:59:59+05:30", "2002-03-31T23:59:59+05:30", true)]
    [InlineData("2011-03-31T23:59:59+05:30", "2002-04-01T00:00:00+05:30", false)]
    [InlineData("2011-04-01T00:00:00+05:30", "2003-03-31T23:59:59+05:30", true)]
    [InlineData("2011-04-01T00:00:00+05:30", "2003-04-01T00:00:00+05:30", false)]
    [InlineData("2011-03-31T19:00:00+00:00", "2003-03-31T23:59:59+05:30", true)]
    public async Task SweepAsync_ChangeRecordAroundTheStartOfAFinancialYearInIst_IsDeletedOnlyWhenItsYearIsPastTheRetention(string clock, string changedAt, bool deleted)
    {
        var id = await AddChangeRecordAsync(DateTimeOffset.Parse(changedAt, CultureInfo.InvariantCulture));

        await SweepAsync(DateTimeOffset.Parse(clock, CultureInfo.InvariantCulture), RetentionYears, new FakeLogger<AuditRetentionSweep>());

        Assert.Equal(!deleted, await ChangeRecordExistsAsync(id));
    }

    [Theory]
    [InlineData("2035-06-15T12:00:00+05:30", 8, "2027-04-01T00:00:00+05:30")]
    [InlineData("2036-03-31T23:59:59+05:30", 8, "2027-04-01T00:00:00+05:30")]
    [InlineData("2036-04-01T00:00:00+05:30", 8, "2028-04-01T00:00:00+05:30")]
    [InlineData("2026-10-16T10:00:00+05:30", 1, "2025-04-01T00:00:00+05:30")]
    [InlineData("2026-10-16T10:00:00+05:30", 30, "1996-04-01T00:00:00+05:30")]
    public void ChangeRecordsBefore_ClockAndYears_IsTheStartInIstOfTheFirstFinancialYearKept(string clock, int years, string expected)
    {
        var cutoff = AuditRetentionSweep.ChangeRecordsBefore(DateTimeOffset.Parse(clock, CultureInfo.InvariantCulture), years);

        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), cutoff);
    }

    [Fact]
    public async Task SweepAsync_NoChangeRecordRetention_KeepsEveryChangeRecordAndSaysSo()
    {
        var id = await AddChangeRecordAsync(new DateTimeOffset(1999, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var logger = new FakeLogger<AuditRetentionSweep>();
        await SweepAsync(new DateTimeOffset(2010, 6, 15, 0, 0, 0, TimeSpan.Zero), changeRecordRetentionYears: null, logger);

        Assert.True(await ChangeRecordExistsAsync(id));
        Assert.Contains(logger.Collector.GetSnapshot(), record => record.Message.StartsWith("Kept every change record", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SweepAsync_MoreRowsThanABatch_DeletesThemAllInDeleteTopStatementsOfOneBatchEach()
    {
        var now = new DateTimeOffset(2010, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var cutoff = AuditRetentionSweep.ChangeRecordsBefore(now, RetentionYears);
        var marker = await AddChangeRecordsAsync(AuditRetentionSweep.BatchSize + 1, new DateTimeOffset(1998, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var due = await CountChangeRecordsBeforeAsync(cutoff);

        var commands = new CommandLog();
        var logger = new FakeLogger<AuditRetentionSweep>();
        await SweepAsync(now, RetentionYears, logger, commands);

        Assert.Equal(0, await CountMarkedChangeRecordsAsync(marker));
        var deletions = commands.Commands.Where(command => command.Contains("[audit].[ChangeRecords]", StringComparison.Ordinal)).ToList();
        Assert.Equal((due / AuditRetentionSweep.BatchSize) + 1, deletions.Count);
        Assert.All(deletions, command => Assert.StartsWith("DELETE TOP(", command, StringComparison.Ordinal));
        Assert.Equal(due.ToString(CultureInfo.InvariantCulture), DeletedCount(logger, "change records"));
    }

    private static string? DeletedCount(FakeLogger<AuditRetentionSweep> logger, string rows) =>
        Assert.Single(logger.Collector.GetSnapshot(), record => record.Message.StartsWith("Deleted", StringComparison.Ordinal) && record.Message.Contains(rows, StringComparison.Ordinal))
            .GetStructuredStateValue("Count");

    private async Task SweepAsync(DateTimeOffset now, int? changeRecordRetentionYears, FakeLogger<AuditRetentionSweep> logger, params IInterceptor[] interceptors)
    {
        await using var context = CreateContext(interceptors);
        var sweep = new AuditRetentionSweep(context, new FakeTimeProvider(now), Options.Create(new AuditingOptions { ChangeRecordRetentionYears = changeRecordRetentionYears }), logger);

        await sweep.SweepAsync(TestContext.Current.CancellationToken);
    }

    private AuditingDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AuditingDbContext>();
        ModuleDbContextRegistration.Configure(options, new DatabaseOptions { ConnectionString = database.ConnectionString }, AuditingDbContext.SchemaName)
            .AddInterceptors([new AuditingSaveChangesInterceptor(database.Actor, database.Origin, database.Clock), new BulkWriteGuardInterceptor(), .. interceptors])
            .ConfigureWarnings(warnings => warnings.Log(CoreEventId.ManyServiceProvidersCreatedWarning));

        return new AuditingDbContext(options.Options);
    }

    private async Task<string> AddSecurityEventAsync(DateTimeOffset occurredAt)
    {
        var correlationId = Guid.CreateVersion7().ToString("N");
        await using var context = CreateContext();
        context.SecurityEvents.Add(SecurityEvent.Record(new SecurityEventEntry(SecurityEventKind.SignInFailed, "protocol:none", CorrelationId: correlationId), occurredAt));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return correlationId;
    }

    private async Task<Guid> AddChangeRecordAsync(DateTimeOffset occurredAt)
    {
        var id = Guid.CreateVersion7();
        await using var context = CreateContext();
        context.ChangeRecords.Add(ChangeRecord.Record(new ChangeStamp(occurredAt, ActorIds.System, null, null), ChangeOperation.Created, ChangeKey, id, ChangeKey, id, null, 0));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task<string> AddChangeRecordsAsync(int count, DateTimeOffset occurredAt)
    {
        var marker = Guid.CreateVersion7().ToString("N");
        await using var context = CreateContext();
        await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO [audit].[ChangeRecords] ([Id], [OccurredAt], [Operation], [EntityType], [EntityId], [SubjectType], [SubjectId], [ActorId], [CorrelationId], [ValueCount])
            SELECT NEWID(), {occurredAt}, 1, {ChangeKey}, NEWID(), {ChangeKey}, NEWID(), {ActorIds.System}, {marker}, 0
            FROM (SELECT TOP ({count}) 1 AS [Row] FROM sys.all_columns AS a CROSS JOIN sys.all_columns AS b) AS [Rows]
            """,
            TestContext.Current.CancellationToken);

        return marker;
    }

    private async Task<bool> SecurityEventExistsAsync(string correlationId)
    {
        await using var context = CreateContext();

        return await context.SecurityEvents.AnyAsync(e => e.CorrelationId == correlationId, TestContext.Current.CancellationToken);
    }

    private async Task<bool> ChangeRecordExistsAsync(Guid entityId)
    {
        await using var context = CreateContext();

        return await context.ChangeRecords.AnyAsync(r => r.EntityId == entityId, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountMarkedChangeRecordsAsync(string marker)
    {
        await using var context = CreateContext();

        return await context.ChangeRecords.CountAsync(r => r.CorrelationId == marker, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountChangeRecordsBeforeAsync(DateTimeOffset cutoff)
    {
        await using var context = CreateContext();

        return await context.ChangeRecords.CountAsync(r => r.OccurredAt < cutoff, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountSecurityEventsBeforeAsync(DateTimeOffset cutoff)
    {
        await using var context = CreateContext();

        return await context.SecurityEvents.CountAsync(e => e.OccurredAt < cutoff, TestContext.Current.CancellationToken);
    }
}
