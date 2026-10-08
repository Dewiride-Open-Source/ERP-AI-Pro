using System.Security.Claims;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Dewiride.Erp.BuildingBlocks.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Sessions;

// An Entra session is one browser's sign-in to Entra, and its id (sid) names it in the id tokens of every application signed
// in within it and in the front-channel sign-out Entra sends each of them when it ends. Every sign-in therefore records the
// person's account under the Entra session it came from, and a session of this API is accepted only while that record names
// its person (SessionCookieEvents), so removing the record ends exactly the sessions signed in from that Entra session. The
// record is the account id as JSON, which is not secret, so it is stored unprotected. It expires once no session signed in
// with it can still be within its lifetime.
internal sealed class EntraSessions(
    [FromKeyedServices(CachingRegistration.SqlServerCacheKey)] IDistributedCache store,
    IOptions<EntraSignInOptions> signIn)
{
    public const string KeyPrefix = "entra-session:";

    public const string MalformedRecordMessage = "The record of a Microsoft Entra ID session names no account.";

    public Task RecordAsync(ClaimsPrincipal person, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (person.FindFirst(JwtRegisteredClaimNames.Sid)?.Value is not { Length: > 0 } sessionId
            || person.GetMsalAccountId() is not { } accountId)
        {
            return Task.CompletedTask;
        }

        return store.SetAsync(
            KeyPrefix + sessionId,
            JsonSerializer.SerializeToUtf8Bytes(new EntraSession(accountId)),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = signIn.Value.SessionLifetime + TokenCacheRegistration.ExpirationMargin },
            cancellationToken);
    }

    public async Task<EntraSession?> FindAsync(string sessionId, CancellationToken cancellationToken)
    {
        var record = await store.GetAsync(KeyPrefix + sessionId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        EntraSession? session;
        try
        {
            session = JsonSerializer.Deserialize<EntraSession>(record);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(MalformedRecordMessage, exception);
        }

        return session is { AccountId.Length: > 0 } ? session : throw new InvalidDataException(MalformedRecordMessage);
    }

    public async Task<bool> HoldsAsync(string sessionId, string accountId, CancellationToken cancellationToken) =>
        await FindAsync(sessionId, cancellationToken).ConfigureAwait(false) is { } session
        && string.Equals(session.AccountId, accountId, StringComparison.Ordinal);

    public Task ForgetAsync(string sessionId, CancellationToken cancellationToken) => store.RemoveAsync(KeyPrefix + sessionId, cancellationToken);
}
