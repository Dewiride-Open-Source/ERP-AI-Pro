using System.Security.Claims;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Sessions;

public sealed class EntraSessionsTests
{
    private const string SessionId = "7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f";

    private const string Issuer = "https://login.microsoftonline.com/5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d/v2.0";

    private const string HomeObjectId = "3f0c2a8e-6b1d-4c7e-9a52-0d8e4f1b7c31";

    private const string HomeTenantId = "5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d";

    private const string AccountId = $"{HomeObjectId}.{HomeTenantId}";

    private readonly RecordingStore _store = new();

    [Fact]
    public async Task RecordAsync_PersonWithAnEntraSession_StoresTheIssuerAndAccountAsJsonUnderTheSessionKey()
    {
        await Sessions().RecordAsync(Person(), TestContext.Current.CancellationToken);

        var write = Assert.Single(_store.Writes);
        Assert.Equal($"entra-session:{SessionId}", write.Key);
        Assert.Equal($$"""{"Issuer":"{{Issuer}}","AccountId":"{{AccountId}}"}""", Encoding.UTF8.GetString(write.Value));
    }

    [Theory]
    [InlineData(null, "12:05:00")]
    [InlineData("02:00:00", "02:05:00")]
    public async Task RecordAsync_PersonWithAnEntraSession_ExpiresTheRecordFiveMinutesAfterTheLastSessionSignedInWithItCouldEnd(string? sessionLifetime, string expiry)
    {
        var signIn = new EntraSignInOptions();
        if (sessionLifetime is not null)
        {
            signIn.SessionLifetime = TimeSpan.Parse(sessionLifetime, System.Globalization.CultureInfo.InvariantCulture);
        }

        await new EntraSessions(_store, Microsoft.Extensions.Options.Options.Create(signIn)).RecordAsync(Person(), TestContext.Current.CancellationToken);

        var options = Assert.Single(_store.Writes).Options;
        Assert.Equal(TimeSpan.Parse(expiry, System.Globalization.CultureInfo.InvariantCulture), options.AbsoluteExpirationRelativeToNow);
        Assert.Null(options.AbsoluteExpiration);
        Assert.Null(options.SlidingExpiration);
    }

    [Theory]
    [InlineData("sid")]
    [InlineData("iss")]
    [InlineData("uid")]
    [InlineData("utid")]
    public async Task RecordAsync_PersonWithoutTheClaim_RecordsNothing(string missing)
    {
        await Sessions().RecordAsync(Person(without: missing), TestContext.Current.CancellationToken);

        Assert.Empty(_store.Writes);
    }

    [Theory]
    [InlineData("sid")]
    [InlineData("iss")]
    public async Task RecordAsync_PersonWithTheClaimEmpty_RecordsNothing(string empty)
    {
        await Sessions().RecordAsync(Person(empty: empty), TestContext.Current.CancellationToken);

        Assert.Empty(_store.Writes);
    }

    [Fact]
    public async Task FindAsync_RecordedSession_ReturnsItsIssuerAndAccount()
    {
        var sessions = Sessions();
        await sessions.RecordAsync(Person(), TestContext.Current.CancellationToken);

        var session = await sessions.FindAsync(SessionId, TestContext.Current.CancellationToken);

        Assert.Equal(new EntraSession(Issuer, AccountId), session);
    }

    [Fact]
    public async Task FindAsync_UnknownSession_ReturnsNothing()
    {
        Assert.Null(await Sessions().FindAsync(SessionId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ForgetAsync_RecordedSession_RemovesTheRecord()
    {
        var sessions = Sessions();
        await sessions.RecordAsync(Person(), TestContext.Current.CancellationToken);

        await sessions.ForgetAsync(SessionId, TestContext.Current.CancellationToken);

        Assert.Equal([$"entra-session:{SessionId}"], _store.Removals);
        Assert.Null(await sessions.FindAsync(SessionId, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"Issuer":"https://login.microsoftonline.com/x/v2.0"}""")]
    [InlineData("""{"Issuer":"","AccountId":"a.b"}""")]
    public async Task FindAsync_RecordThatIsNoIssuerAndAccount_FailsInsteadOfMatchingASession(string record)
    {
        await _store.SetAsync($"entra-session:{SessionId}", Encoding.UTF8.GetBytes(record), new DistributedCacheEntryOptions(), TestContext.Current.CancellationToken);

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => Sessions().FindAsync(SessionId, TestContext.Current.CancellationToken));

        Assert.Equal(EntraSessions.MalformedRecordMessage, failure.Message);
    }

    [Fact]
    public async Task RecordAndFindAsync_StoreFailing_PropagateTheFailure()
    {
        var failure = new TimeoutException("The store did not answer.");
        _store.Failure = failure;

        Assert.Same(failure, await Assert.ThrowsAsync<TimeoutException>(() => Sessions().RecordAsync(Person(), TestContext.Current.CancellationToken)));
        Assert.Same(failure, await Assert.ThrowsAsync<TimeoutException>(() => Sessions().FindAsync(SessionId, TestContext.Current.CancellationToken)));
    }

    private EntraSessions Sessions() => new(_store, Microsoft.Extensions.Options.Options.Create(new EntraSignInOptions()));

    private static ClaimsPrincipal Person(string? without = null, string? empty = null)
    {
        (string Type, string Value)[] claims =
        [
            ("iss", Issuer),
            ("oid", HomeObjectId),
            ("sid", SessionId),
            ("uid", HomeObjectId),
            ("utid", HomeTenantId),
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims.Where(claim => claim.Type != without).Select(claim => new Claim(claim.Type, claim.Type == empty ? string.Empty : claim.Value)),
            "Cookies"));
    }

    private sealed class RecordingStore : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner = new(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

        public List<(string Key, byte[] Value, DistributedCacheEntryOptions Options)> Writes { get; } = [];

        public List<string> Removals { get; } = [];

        public Exception? Failure { get; set; }

        public byte[]? Get(string key) => throw new NotSupportedException("Entra session records are read asynchronously.");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            ThrowIfFailing();
            return _inner.GetAsync(key, token);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException("Entra session records are written asynchronously.");

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            ThrowIfFailing();
            Writes.Add((key, value, options));
            return _inner.SetAsync(key, value, options, token);
        }

        public void Refresh(string key) => throw new NotSupportedException("Entra session records never slide.");

        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException("Entra session records never slide.");

        public void Remove(string key) => throw new NotSupportedException("Entra session records are removed asynchronously.");

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            ThrowIfFailing();
            Removals.Add(key);
            return _inner.RemoveAsync(key, token);
        }

        private void ThrowIfFailing()
        {
            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }
}
