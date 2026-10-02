using Azure;
using Azure.Core.Cryptography;

namespace Dewiride.Erp.Host.Api.IntegrationTests.DataProtection;

// Answers as Key Vault answers an identity that may read the key but not unwrap with it: the key resolves with its version
// and wraps locally with its public part, and the unwrap, which only Key Vault can perform, is refused.
internal sealed class RefusingKeyEncryptionKeyResolver(IKeyEncryptionKeyResolver readable) : IKeyEncryptionKeyResolver
{
    public const int Status = 403;

    public const string ErrorCode = "Forbidden";

    public IKeyEncryptionKey Resolve(string keyId, CancellationToken cancellationToken = default) => new UnwrapRefusingKey(readable.Resolve(keyId, cancellationToken));

    public async Task<IKeyEncryptionKey> ResolveAsync(string keyId, CancellationToken cancellationToken = default) =>
        new UnwrapRefusingKey(await readable.ResolveAsync(keyId, cancellationToken));

    private sealed class UnwrapRefusingKey(IKeyEncryptionKey readable) : IKeyEncryptionKey
    {
        public string KeyId => readable.KeyId;

        public byte[] WrapKey(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default) =>
            readable.WrapKey(algorithm, key, cancellationToken);

        public Task<byte[]> WrapKeyAsync(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default) =>
            readable.WrapKeyAsync(algorithm, key, cancellationToken);

        public byte[] UnwrapKey(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default) => throw Refusal();

        public Task<byte[]> UnwrapKeyAsync(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default) =>
            Task.FromException<byte[]>(Refusal());

        private static RequestFailedException Refusal() =>
            new(Status, "Caller is not authorized to perform action on resource.", ErrorCode, innerException: null);
    }
}
