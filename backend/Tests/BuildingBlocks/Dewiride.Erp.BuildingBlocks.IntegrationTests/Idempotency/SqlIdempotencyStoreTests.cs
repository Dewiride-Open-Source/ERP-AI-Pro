using System.Security.Cryptography;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Idempotency.Persistence;
using Dewiride.Erp.BuildingBlocks.Idempotency.Storage;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Idempotency;

public sealed class SqlIdempotencyStoreTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private static readonly Guid Actor = new("0199a1b2-0000-7000-8000-0000000000aa");

    private static readonly byte[] Fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes("request"));

    private static readonly byte[] OtherFingerprint = SHA256.HashData(Encoding.UTF8.GetBytes("other request"));

    [Fact]
    public async Task BeginAsync_NewKey_StartsAndStoresAnInProgressRow()
    {
        var key = Guid.CreateVersion7();
        var now = database.Clock.GetUtcNow();

        var outcome = await BeginAsync(key, Fingerprint, now);

        Assert.Equal(BeginOutcome.Started, outcome);
        var record = await FindAsync(key);
        Assert.Equal(IdempotencyStatus.InProgress, record.Status);
        Assert.Equal(now, record.CreatedAt);
        Assert.Equal(now.AddDays(1), record.ExpiresAt);
        Assert.Null(record.StatusCode);
        Assert.Null(record.Body);
    }

    [Fact]
    public async Task BeginAsync_KeyInProgress_ReportsInProgressForTheSameFingerprint()
    {
        var key = Guid.CreateVersion7();
        await BeginAsync(key, Fingerprint, database.Clock.GetUtcNow());

        var outcome = await BeginAsync(key, Fingerprint, database.Clock.GetUtcNow());

        Assert.Equal(BeginState.InProgress, outcome.State);
        Assert.Equal(key, outcome.Existing!.Key);
    }

    [Fact]
    public async Task BeginAsync_KeyUsedWithAnotherFingerprint_ReportsTheMismatch()
    {
        var key = Guid.CreateVersion7();
        await BeginAsync(key, Fingerprint, database.Clock.GetUtcNow());

        var outcome = await BeginAsync(key, OtherFingerprint, database.Clock.GetUtcNow());

        Assert.Equal(BeginState.FingerprintMismatch, outcome.State);
    }

    [Fact]
    public async Task CompleteAsync_ThenBegin_ReplaysTheStoredResponse()
    {
        var key = Guid.CreateVersion7();
        var now = database.Clock.GetUtcNow();
        await BeginAsync(key, Fingerprint, now);
        var body = Encoding.UTF8.GetBytes("{\"ok\":true}");

        await using (var scope = database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().CompleteAsync(Actor, key, new StoredResponse(201, "application/json", "/api/things/1", body), now.AddSeconds(1), TestContext.Current.CancellationToken);
        }

        var outcome = await BeginAsync(key, Fingerprint, now.AddSeconds(2));
        Assert.Equal(BeginState.Completed, outcome.State);
        Assert.Equal(201, outcome.Existing!.StatusCode);
        Assert.Equal("application/json", outcome.Existing.ContentType);
        Assert.Equal("/api/things/1", outcome.Existing.Location);
        Assert.Equal(body, outcome.Existing.Body);
        Assert.Equal(now.AddSeconds(1), outcome.Existing.CompletedAt);
    }

    [Fact]
    public async Task AbandonAsync_InProgressKey_RemovesTheRowSoTheKeyCanBeClaimedAgain()
    {
        var key = Guid.CreateVersion7();
        await BeginAsync(key, Fingerprint, database.Clock.GetUtcNow());

        await using (var scope = database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().AbandonAsync(Actor, key, TestContext.Current.CancellationToken);
        }

        Assert.Equal(BeginOutcome.Started, await BeginAsync(key, OtherFingerprint, database.Clock.GetUtcNow()));
    }

    [Fact]
    public async Task BeginAsync_ExpiredKey_IsReplacedByTheNewRequest()
    {
        var key = Guid.CreateVersion7();
        var now = database.Clock.GetUtcNow();
        await BeginAsync(key, Fingerprint, now);

        var outcome = await BeginAsync(key, OtherFingerprint, now.AddDays(1).AddSeconds(1));

        Assert.Equal(BeginOutcome.Started, outcome);
        Assert.Equal(OtherFingerprint, (await FindAsync(key)).Fingerprint);
    }

    [Fact]
    public async Task BeginAsync_SameKeyForAnotherActor_IsIndependent()
    {
        var key = Guid.CreateVersion7();
        await BeginAsync(key, Fingerprint, database.Clock.GetUtcNow());

        await using var scope = database.CreateScope();
        var outcome = await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().BeginAsync(Guid.CreateVersion7(), key, OtherFingerprint, database.Clock.GetUtcNow(), database.Clock.GetUtcNow().AddDays(1), TestContext.Current.CancellationToken);

        Assert.Equal(BeginOutcome.Started, outcome);
    }

    [Fact]
    public async Task DeleteExpiredAsync_RemovesOnlyRowsPastTheirExpiry()
    {
        var expired = Guid.CreateVersion7();
        var live = Guid.CreateVersion7();
        var now = database.Clock.GetUtcNow();
        await BeginAsync(expired, Fingerprint, now.AddDays(-2));
        await BeginAsync(live, Fingerprint, now);

        await using (var scope = database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().DeleteExpiredAsync(now, TestContext.Current.CancellationToken);
        }

        await using var check = database.CreateScope();
        var records = check.ServiceProvider.GetRequiredService<IdempotencyDbContext>().Records;
        Assert.False(await records.AnyAsync(r => r.Key == expired, TestContext.Current.CancellationToken));
        Assert.True(await records.AnyAsync(r => r.Key == live, TestContext.Current.CancellationToken));
    }

    private async Task<BeginOutcome> BeginAsync(Guid key, byte[] fingerprint, DateTimeOffset now)
    {
        await using var scope = database.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().BeginAsync(Actor, key, fingerprint, now, now.AddDays(1), TestContext.Current.CancellationToken);
    }

    private async Task<IdempotencyRecord> FindAsync(Guid key)
    {
        await using var scope = database.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>().Records.AsNoTracking().SingleAsync(r => r.ActorId == Actor && r.Key == key, TestContext.Current.CancellationToken);
    }
}
