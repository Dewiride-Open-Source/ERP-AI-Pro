using System.Globalization;
using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.TokenCache;

public sealed class SessionBoundTokenCacheStoreTests
{
    private const string ObjectId = "3f0c2a8e-6b1d-4c7e-9a52-0d8e4f1b7c31";

    private const string TenantId = "5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d";

    private const string AccountId = $"{ObjectId}.{TenantId}";

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    private static readonly DateTimeOffset SignedInAt = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly DistributedCacheEntryOptions AdapterExpiry = new()
    {
        AbsoluteExpirationRelativeToNow = Lifetime + TokenCacheRegistration.ExpirationMargin,
        SlidingExpiration = IdleTimeout + TokenCacheRegistration.ExpirationMargin,
    };

    private readonly FakeTimeProvider _clock = new(SignedInAt);

    private readonly RecordingStore _inner = new();

    private readonly HttpContextAccessor _httpContext = new();

    [Fact]
    public async Task SetAsync_WrittenLateInThePersonsOwnSession_ExpiresWhenTheSessionEnds()
    {
        _httpContext.HttpContext = Session(AccountId, SignedInAt);
        _clock.Advance(TimeSpan.FromHours(11));

        await Store().SetAsync(AccountId, [1], AdapterExpiry, TestContext.Current.CancellationToken);

        var written = Assert.Single(_inner.Writes);
        Assert.Equal(SignedInAt + Lifetime + TokenCacheRegistration.ExpirationMargin, written.AbsoluteExpiration);
        Assert.Null(written.AbsoluteExpirationRelativeToNow);
        Assert.Equal(AdapterExpiry.SlidingExpiration, written.SlidingExpiration);
    }

    [Fact]
    public void Set_WrittenLateInThePersonsOwnSession_ExpiresWhenTheSessionEnds()
    {
        _httpContext.HttpContext = Session(AccountId, SignedInAt);
        _clock.Advance(TimeSpan.FromHours(11));

        Store().Set(AccountId, [1], AdapterExpiry);

        Assert.Equal(SignedInAt + Lifetime + TokenCacheRegistration.ExpirationMargin, Assert.Single(_inner.Writes).AbsoluteExpiration);
    }

    [Fact]
    public async Task SetAsync_WithoutASession_KeepsTheExpiryOfTheAdapter()
    {
        _httpContext.HttpContext = new DefaultHttpContext();

        await Store().SetAsync(AccountId, [1], AdapterExpiry, TestContext.Current.CancellationToken);

        Assert.Same(AdapterExpiry, Assert.Single(_inner.Writes));
    }

    [Fact]
    public async Task SetAsync_OutsideARequest_KeepsTheExpiryOfTheAdapter()
    {
        await Store().SetAsync(AccountId, [1], AdapterExpiry, TestContext.Current.CancellationToken);

        Assert.Same(AdapterExpiry, Assert.Single(_inner.Writes));
    }

    [Fact]
    public async Task SetAsync_OfAnotherPersonsEntry_KeepsTheExpiryOfTheAdapter()
    {
        _httpContext.HttpContext = Session(AccountId, SignedInAt);
        _clock.Advance(TimeSpan.FromHours(11));

        await Store().SetAsync($"{Guid.CreateVersion7():D}.{TenantId}", [1], AdapterExpiry, TestContext.Current.CancellationToken);

        Assert.Same(AdapterExpiry, Assert.Single(_inner.Writes));
    }

    [Fact]
    public async Task SetAsync_ExpiringBeforeTheSessionEnds_KeepsTheEarlierExpiryAsAnAbsoluteTime()
    {
        _httpContext.HttpContext = Session(AccountId, SignedInAt);
        var shorter = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10), SlidingExpiration = IdleTimeout };

        await Store().SetAsync(AccountId, [1], shorter, TestContext.Current.CancellationToken);

        var written = Assert.Single(_inner.Writes);
        Assert.Equal(SignedInAt + TimeSpan.FromMinutes(10), written.AbsoluteExpiration);
        Assert.Null(written.AbsoluteExpirationRelativeToNow);
        Assert.Equal(IdleTimeout, written.SlidingExpiration);
    }

    [Fact]
    public async Task SetAsync_WithoutAnAbsoluteExpiry_ExpiresWhenTheSessionEnds()
    {
        _httpContext.HttpContext = Session(AccountId, SignedInAt);
        var sliding = new DistributedCacheEntryOptions { SlidingExpiration = IdleTimeout };

        await Store().SetAsync(AccountId, [1], sliding, TestContext.Current.CancellationToken);

        var written = Assert.Single(_inner.Writes);
        Assert.Equal(SignedInAt + Lifetime + TokenCacheRegistration.ExpirationMargin, written.AbsoluteExpiration);
        Assert.Equal(IdleTimeout, written.SlidingExpiration);
    }

    [Fact]
    public async Task SetAsync_AfterTheSessionHasEnded_KeepsTheExpiryOfTheAdapter()
    {
        _httpContext.HttpContext = Session(AccountId, SignedInAt);
        _clock.Advance(Lifetime + TokenCacheRegistration.ExpirationMargin);

        await Store().SetAsync(AccountId, [1], AdapterExpiry, TestContext.Current.CancellationToken);

        Assert.Same(AdapterExpiry, Assert.Single(_inner.Writes));
    }

    private static DefaultHttpContext Session(string accountId, DateTimeOffset signedInAt)
    {
        var parts = accountId.Split('.');
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("uid", parts[0]), new Claim("utid", parts[1])], "Cookies"));
        var properties = new AuthenticationProperties();
        properties.Items[SessionCookieEvents.SignedInAtItem] = signedInAt.ToString("O", CultureInfo.InvariantCulture);
        var context = new DefaultHttpContext { User = principal };
        context.Features.Set<IAuthenticateResultFeature>(new AuthenticateResultFeature(AuthenticateResult.Success(new AuthenticationTicket(principal, properties, "Cookies"))));

        return context;
    }

    private SessionBoundTokenCacheStore Store() =>
        new(_inner, _httpContext, Microsoft.Extensions.Options.Options.Create(new EntraSignInOptions { SessionLifetime = Lifetime, SessionIdleTimeout = IdleTimeout }), _clock);

    private sealed class AuthenticateResultFeature(AuthenticateResult result) : IAuthenticateResultFeature
    {
        public AuthenticateResult? AuthenticateResult { get; set; } = result;
    }

    private sealed class RecordingStore : IDistributedCache
    {
        public List<DistributedCacheEntryOptions> Writes { get; } = [];

        public byte[]? Get(string key) => null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Writes.Add(options);

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Writes.Add(options);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key)
        {
        }

        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }
}
