using System.Runtime.ExceptionServices;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Caching;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web.TokenCacheProviders;
using Microsoft.Identity.Web.TokenCacheProviders.Distributed;

namespace Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;

// The token cache holds one entry per person in SQL Server, the entry that signing out removes and the session check reads,
// so it outlives a restart of the API. The adapter is built by hand over the keyed SQL Server cache: AddDistributedTokenCaches
// would build it over an unkeyed in-memory IDistributedCache that it registers itself.
internal static class TokenCacheRegistration
{
    // Covers the moment between redeeming the code and stamping the sign-in time, and the clock difference between instances.
    public static readonly TimeSpan ExpirationMargin = TimeSpan.FromMinutes(5);

    public static IServiceCollection AddErpTokenCache(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddOptions<MsalDistributedTokenCacheAdapterOptions>().Configure<IOptions<EntraSignInOptions>>(static (cache, signIn) =>
        {
            // The memory level never checks SQL Server again, so another instance would keep accepting a person who signed out.
            cache.DisableL1Cache = true;

            // ProtectedTokenCacheStore protects every entry below the adapter.
            cache.Encrypt = false;

            // An entry expires the margin after its session ends: the idle timeout after the person's last request, or the
            // lifetime after sign-in, whichever comes first.
            cache.AbsoluteExpirationRelativeToNow = signIn.Value.SessionLifetime + ExpirationMargin;
            cache.SlidingExpiration = signIn.Value.SessionIdleTimeout + ExpirationMargin;

            // The adapter catches every store failure and takes a failed read for an empty cache, which would sign the person
            // out on a passing SQL error; an exception thrown from this callback is the only one that leaves its catch block. A
            // cancellation leaves as itself, so a request that timed out or was abandoned answers 504 or 499 as elsewhere.
            cache.OnL2CacheFailure = static exception =>
            {
                if (exception is OperationCanceledException)
                {
                    ExceptionDispatchInfo.Throw(exception);
                }

                throw new TokenCacheUnavailableException(exception);
            };
        });

        services.AddSingleton<IMsalTokenCacheProvider>(static provider => new MsalDistributedTokenCacheAdapter(
            new SessionBoundTokenCacheStore(
                new ProtectedTokenCacheStore(
                    provider.GetRequiredKeyedService<IDistributedCache>(CachingRegistration.SqlServerCacheKey),
                    provider.GetRequiredService<IDataProtectionProvider>(),
                    provider.GetRequiredService<IHttpContextAccessor>(),
                    provider.GetRequiredService<ILogger<ProtectedTokenCacheStore>>()),
                provider.GetRequiredService<IHttpContextAccessor>(),
                provider.GetRequiredService<IOptions<EntraSignInOptions>>(),
                provider.GetRequiredService<TimeProvider>()),
            provider.GetRequiredService<IOptions<MsalDistributedTokenCacheAdapterOptions>>(),
            provider.GetRequiredService<ILogger<MsalDistributedTokenCacheAdapter>>(),
            provider));

        return services;
    }
}
