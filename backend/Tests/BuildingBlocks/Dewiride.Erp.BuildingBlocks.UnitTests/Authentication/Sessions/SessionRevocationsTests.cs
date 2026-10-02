using System.Buffers.Binary;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Dewiride.Erp.BuildingBlocks.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Sessions;

public sealed class SessionRevocationsTests
{
    private const string AccountId = "3f0c2a8e-6b1d-4c7e-9a52-0d8e4f1b7c31.5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d";

    private const string OtherAccountId = "8b6e1f47-2c9a-4d3b-b5e0-7a19c4d2e865.5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d";

    private static readonly DateTimeOffset SignedOutAt = new(2026, 10, 2, 9, 30, 15, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(SignedOutAt);

    private readonly RecordingStore _store = new();

    [Fact]
    public async Task IsRevokedAsync_WithoutASignOut_IsFalse()
    {
        Assert.False(await Revocations().IsRevokedAsync(AccountId, SignedOutAt.AddHours(-1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsRevokedAsync_SessionIssuedBeforeTheSignOut_IsTrue()
    {
        var revocations = Revocations();
        await revocations.RevokeAsync(AccountId, TestContext.Current.CancellationToken);

        Assert.True(await revocations.IsRevokedAsync(AccountId, SignedOutAt.AddMinutes(-20), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsRevokedAsync_SessionIssuedAtTheSignOut_IsTrue()
    {
        var revocations = Revocations();
        await revocations.RevokeAsync(AccountId, TestContext.Current.CancellationToken);

        Assert.True(await revocations.IsRevokedAsync(AccountId, SignedOutAt, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsRevokedAsync_SessionIssuedATickAfterTheSignOut_IsFalse()
    {
        var revocations = Revocations();
        await revocations.RevokeAsync(AccountId, TestContext.Current.CancellationToken);

        Assert.False(await revocations.IsRevokedAsync(AccountId, SignedOutAt.AddTicks(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsRevokedAsync_SignInTimeInAnotherOffset_ComparesTheSameInstant()
    {
        var revocations = Revocations();
        await revocations.RevokeAsync(AccountId, TestContext.Current.CancellationToken);

        Assert.True(await revocations.IsRevokedAsync(AccountId, SignedOutAt.ToOffset(TimeSpan.FromHours(5.5)), TestContext.Current.CancellationToken));
        Assert.False(await revocations.IsRevokedAsync(AccountId, SignedOutAt.AddSeconds(1).ToOffset(TimeSpan.FromHours(5.5)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsRevokedAsync_AnotherPersonSignedOut_IsFalse()
    {
        var revocations = Revocations();
        await revocations.RevokeAsync(OtherAccountId, TestContext.Current.CancellationToken);

        Assert.False(await revocations.IsRevokedAsync(AccountId, SignedOutAt.AddMinutes(-20), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevokeAsync_SignOutLater_RefusesTheSessionsIssuedInBetween()
    {
        var revocations = Revocations();
        await revocations.RevokeAsync(AccountId, TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromMinutes(10));

        await revocations.RevokeAsync(AccountId, TestContext.Current.CancellationToken);

        Assert.True(await revocations.IsRevokedAsync(AccountId, SignedOutAt.AddMinutes(5), TestContext.Current.CancellationToken));
        Assert.False(await revocations.IsRevokedAsync(AccountId, SignedOutAt.AddMinutes(11), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevokeAsync_SignOut_StoresTheSignOutTimeAsBigEndianUtcTicksUnderTheSignedOutKeyOfTheAccount()
    {
        await Revocations().RevokeAsync(AccountId, TestContext.Current.CancellationToken);

        var write = Assert.Single(_store.Writes);
        Assert.Equal($"signed-out:{AccountId}", write.Key);
        Assert.Equal(sizeof(long), write.Value.Length);
        Assert.Equal(SignedOutAt.UtcTicks, BinaryPrimitives.ReadInt64BigEndian(write.Value));
    }

    [Theory]
    [InlineData(null, "12:05:00")]
    [InlineData("02:00:00", "02:05:00")]
    public async Task RevokeAsync_SignOut_ExpiresTheRecordFiveMinutesAfterTheLastSessionIssuedBeforeItCouldEnd(string? sessionLifetime, string expiry)
    {
        var signIn = new EntraSignInOptions();
        if (sessionLifetime is not null)
        {
            signIn.SessionLifetime = TimeSpan.Parse(sessionLifetime, System.Globalization.CultureInfo.InvariantCulture);
        }

        await new SessionRevocations(_store, _clock, Microsoft.Extensions.Options.Options.Create(signIn)).RevokeAsync(AccountId, TestContext.Current.CancellationToken);

        var options = Assert.Single(_store.Writes).Options;
        Assert.Equal(TimeSpan.Parse(expiry, System.Globalization.CultureInfo.InvariantCulture), options.AbsoluteExpirationRelativeToNow);
        Assert.Null(options.AbsoluteExpiration);
        Assert.Null(options.SlidingExpiration);
    }

    [Fact]
    public async Task IsRevokedAsync_RecordThatIsNoSignOutTime_FailsInsteadOfAcceptingTheSession()
    {
        await _store.SetAsync($"signed-out:{AccountId}", [1, 2, 3], new DistributedCacheEntryOptions(), TestContext.Current.CancellationToken);

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => Revocations().IsRevokedAsync(AccountId, SignedOutAt, TestContext.Current.CancellationToken));

        Assert.Equal(SessionRevocations.MalformedRecordMessage, failure.Message);
        Assert.DoesNotContain(AccountId, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IsRevokedAsync_StoreFailing_PropagatesTheFailure()
    {
        var failure = new TimeoutException("The store did not answer.");
        _store.Failure = failure;

        Assert.Same(failure, await Assert.ThrowsAsync<TimeoutException>(() => Revocations().IsRevokedAsync(AccountId, SignedOutAt, TestContext.Current.CancellationToken)));
        Assert.Same(failure, await Assert.ThrowsAsync<TimeoutException>(() => Revocations().RevokeAsync(AccountId, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task RevokeAndIsRevokedAsync_ResolvedWithLoggingAtEveryLevel_WriteNoRecordContainingTheAccountId()
    {
        var services = new ServiceCollection();
        services.AddFakeLogging();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddKeyedSingleton<IDistributedCache>(CachingRegistration.SqlServerCacheKey, _store);
        services.AddSingleton<TimeProvider>(_clock);
        services.AddOptions<EntraSignInOptions>();
        services.AddSingleton<SessionRevocations>();
        await using var provider = services.BuildServiceProvider();
        var revocations = provider.GetRequiredService<SessionRevocations>();

        await revocations.RevokeAsync(AccountId, TestContext.Current.CancellationToken);
        var revoked = await revocations.IsRevokedAsync(AccountId, SignedOutAt.AddMinutes(-1), TestContext.Current.CancellationToken);

        Assert.True(revoked);
        Assert.Equal($"signed-out:{AccountId}", Assert.Single(_store.Writes).Key);
        Assert.DoesNotContain(provider.GetRequiredService<FakeLogCollector>().GetSnapshot(), record =>
            record.Message.Contains(AccountId, StringComparison.Ordinal)
            || (record.StructuredState?.Any(pair => pair.Value?.Contains(AccountId, StringComparison.Ordinal) ?? false) ?? false));
    }

    private SessionRevocations Revocations() => new(_store, _clock, Microsoft.Extensions.Options.Options.Create(new EntraSignInOptions()));

    private sealed class RecordingStore : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner = new(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

        public List<(string Key, byte[] Value, DistributedCacheEntryOptions Options)> Writes { get; } = [];

        public Exception? Failure { get; set; }

        public byte[]? Get(string key) => throw new NotSupportedException("Sign-out records are read asynchronously.");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            ThrowIfFailing();
            return _inner.GetAsync(key, token);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException("Sign-out records are written asynchronously.");

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            ThrowIfFailing();
            Writes.Add((key, value, options));
            return _inner.SetAsync(key, value, options, token);
        }

        public void Refresh(string key) => throw new NotSupportedException("Sign-out records never slide.");

        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException("Sign-out records never slide.");

        public void Remove(string key) => throw new NotSupportedException("Sign-out records are never removed.");

        public Task RemoveAsync(string key, CancellationToken token = default) => throw new NotSupportedException("Sign-out records are never removed.");

        private void ThrowIfFailing()
        {
            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }
}
