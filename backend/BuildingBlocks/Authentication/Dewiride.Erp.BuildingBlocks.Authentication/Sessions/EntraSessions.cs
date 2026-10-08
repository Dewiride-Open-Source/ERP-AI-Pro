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

// Entra ends an Entra session by loading the front-channel logout URL with the issuer and the id of that session (iss and
// sid), from a hidden frame of its own site that never carries the SameSite=Lax session cookie, so every sign-in records the
// person's account under the Entra session it came from. The record is the issuer and the account id as JSON, neither
// secret, so it is stored unprotected. It expires once no session signed in with it can still be within its lifetime.
internal sealed class EntraSessions(
    [FromKeyedServices(CachingRegistration.SqlServerCacheKey)] IDistributedCache store,
    IOptions<EntraSignInOptions> signIn)
{
    public const string KeyPrefix = "entra-session:";

    public const string MalformedRecordMessage = "The record of a Microsoft Entra ID session is not an issuer and an account.";

    public Task RecordAsync(ClaimsPrincipal person, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (person.FindFirst(JwtRegisteredClaimNames.Sid)?.Value is not { Length: > 0 } sessionId
            || person.FindFirst(JwtRegisteredClaimNames.Iss)?.Value is not { Length: > 0 } issuer
            || person.GetMsalAccountId() is not { } accountId)
        {
            return Task.CompletedTask;
        }

        return store.SetAsync(
            KeyPrefix + sessionId,
            JsonSerializer.SerializeToUtf8Bytes(new EntraSession(issuer, accountId)),
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

        return session is { Issuer.Length: > 0, AccountId.Length: > 0 } ? session : throw new InvalidDataException(MalformedRecordMessage);
    }

    public Task ForgetAsync(string sessionId, CancellationToken cancellationToken) => store.RemoveAsync(KeyPrefix + sessionId, cancellationToken);
}
