using System.Buffers.Binary;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Dewiride.Erp.BuildingBlocks.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Sessions;

// Signing out records when the person signed out, so every session issued before then stays refused on every instance of
// the API after the person signs in again, which puts their account back in the token cache. The record is the sign-out
// time in UTC ticks, as precise as the sign-in time a session carries, and it is not secret, so it is stored unprotected. It
// expires once no session issued before it can still be within its lifetime.
internal sealed class SessionRevocations(
    [FromKeyedServices(CachingRegistration.SqlServerCacheKey)] IDistributedCache store,
    TimeProvider timeProvider,
    IOptions<EntraSignInOptions> signIn)
{
    public const string KeyPrefix = "signed-out:";

    public const string MalformedRecordMessage = "The sign-out record of a person is not a sign-out time.";

    private const int RecordLength = sizeof(long);

    public Task RevokeAsync(string accountId, CancellationToken cancellationToken)
    {
        var record = new byte[RecordLength];
        BinaryPrimitives.WriteInt64BigEndian(record, timeProvider.GetUtcNow().UtcTicks);

        return store.SetAsync(
            KeyPrefix + accountId,
            record,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = signIn.Value.SessionLifetime + TokenCacheRegistration.ExpirationMargin },
            cancellationToken);
    }

    public async Task<bool> IsRevokedAsync(string accountId, DateTimeOffset signedInAt, CancellationToken cancellationToken)
    {
        var record = await store.GetAsync(KeyPrefix + accountId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return false;
        }

        if (record.Length != RecordLength)
        {
            throw new InvalidDataException(MalformedRecordMessage);
        }

        return signedInAt.UtcTicks <= BinaryPrimitives.ReadInt64BigEndian(record);
    }
}
