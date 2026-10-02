using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;

// Protects below Microsoft.Identity.Web's adapter rather than through its Encrypt option, which hands an entry it cannot
// decrypt to MSAL as it is and fails every request of that person: an entry written under another key ring reads as missing
// here, so the person counts as signed out and the next sign-in overwrites it. Data Protection reports a key ring it cannot
// reach (Key Vault or the key folder failing) as a CryptographicException wrapping the cause, and a payload no key opens as one
// without an inner exception, so only the latter reads as missing; every other failure reaches the adapter.
// A read that fails leaves MSAL's in-memory copy of the cache unloaded, empty at the start of a request, and MSAL still reports
// that copy when the operation fails: the adapter would delete the stored entry, ending the person's other sessions, or
// overwrite it with a copy it never read. So once a read of a key fails, writing or removing that key fails for the rest of
// the request and the stored entry survives; outside a request nothing is refused. MSAL hands the store no cancellation token,
// so the request's own, which its timeout and a closed connection cancel, bounds every operation and its retries.
internal sealed partial class ProtectedTokenCacheStore(
    IDistributedCache store,
    IDataProtectionProvider dataProtection,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ProtectedTokenCacheStore> logger) : IDistributedCache
{
    public const string Purpose = "Dewiride.Erp.Authentication.TokenCache";

    public const string ChangeAfterFailedReadMessage = "A token cache entry whose read failed in this request is neither written nor removed in it.";

    private static readonly object FailedReadsItem = new();

    private readonly IDataProtector _protector = dataProtection.CreateProtector(Purpose);

    public byte[]? Get(string key)
    {
        try
        {
            return Unprotect(store.Get(key));
        }
        catch (Exception) when (RecordFailedRead(key))
        {
            throw;
        }
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        try
        {
            return Unprotect(await store.GetAsync(key, BoundToTheRequest(token)).ConfigureAwait(false));
        }
        catch (Exception) when (RecordFailedRead(key))
        {
            throw;
        }
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        ThrowIfReadFailed(key);
        store.Set(key, _protector.Protect(value), options);
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        ThrowIfReadFailed(key);
        await store.SetAsync(key, _protector.Protect(value), options, BoundToTheRequest(token)).ConfigureAwait(false);
    }

    public void Refresh(string key) => store.Refresh(key);

    public Task RefreshAsync(string key, CancellationToken token = default) => store.RefreshAsync(key, BoundToTheRequest(token));

    public void Remove(string key)
    {
        ThrowIfReadFailed(key);
        store.Remove(key);
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        ThrowIfReadFailed(key);
        await store.RemoveAsync(key, BoundToTheRequest(token)).ConfigureAwait(false);
    }

    private CancellationToken BoundToTheRequest(CancellationToken token) =>
        token.CanBeCanceled ? token : httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

    private byte[]? Unprotect(byte[]? protectedValue)
    {
        if (protectedValue is null)
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (CryptographicException exception) when (exception.InnerException is null)
        {
            LogUnreadableEntry(logger);

            return null;
        }
    }

    private bool RecordFailedRead(string key)
    {
        if (httpContextAccessor.HttpContext is { } httpContext)
        {
            if (httpContext.Items[FailedReadsItem] is not HashSet<string> failedReads)
            {
                failedReads = new HashSet<string>(StringComparer.Ordinal);
                httpContext.Items[FailedReadsItem] = failedReads;
            }

            failedReads.Add(key);
        }

        // The filter never catches, so the read's own exception leaves with its stack.
        return false;
    }

    private void ThrowIfReadFailed(string key)
    {
        if (httpContextAccessor.HttpContext?.Items[FailedReadsItem] is HashSet<string> failedReads && failedReads.Contains(key))
        {
            throw new InvalidOperationException(ChangeAfterFailedReadMessage);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A token cache entry could not be decrypted with the Data Protection key ring, so its person counts as signed out")]
    private static partial void LogUnreadableEntry(ILogger logger);
}
