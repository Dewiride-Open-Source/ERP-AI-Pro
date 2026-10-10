using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;

// Microsoft.Identity.Web's adapter gives every write the same absolute expiry, counted from the write, so the person's entry
// written again during a session, when the API redeems the session's refresh token for Microsoft Graph, would outlive the
// session. A write made during a request of the person's own session therefore expires no later than the session itself,
// the margin included; the sign-in's own write, before any session exists, keeps the adapter's expiry.
internal sealed class SessionBoundTokenCacheStore(
    IDistributedCache store,
    IHttpContextAccessor httpContextAccessor,
    IOptions<EntraSignInOptions> signIn,
    TimeProvider timeProvider) : IDistributedCache
{
    public byte[]? Get(string key) => store.Get(key);

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => store.GetAsync(key, token);

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => store.Set(key, value, BoundToTheSession(key, options));

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
        store.SetAsync(key, value, BoundToTheSession(key, options), token);

    public void Refresh(string key) => store.Refresh(key);

    public Task RefreshAsync(string key, CancellationToken token = default) => store.RefreshAsync(key, token);

    public void Remove(string key) => store.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default) => store.RemoveAsync(key, token);

    private DistributedCacheEntryOptions BoundToTheSession(string key, DistributedCacheEntryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (httpContextAccessor.HttpContext?.Features.Get<IAuthenticateResultFeature>()?.AuthenticateResult is not { Succeeded: true } session
            || !string.Equals(session.Principal.GetMsalAccountId(), key, StringComparison.Ordinal)
            || SessionCookieEvents.SignedInAt(session.Properties) is not { } signedInAt)
        {
            return options;
        }

        var now = timeProvider.GetUtcNow();
        var sessionEnd = signedInAt + signIn.Value.SessionLifetime + TokenCacheRegistration.ExpirationMargin;
        if (sessionEnd <= now)
        {
            return options;
        }

        var requested = options.AbsoluteExpiration ?? now + options.AbsoluteExpirationRelativeToNow;

        return new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = requested is { } absolute && absolute < sessionEnd ? absolute : sessionEnd,
            SlidingExpiration = options.SlidingExpiration,
        };
    }
}
