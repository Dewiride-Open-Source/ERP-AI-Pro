using System.Security.Cryptography;
using Azure.Core.Cryptography;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace Dewiride.Erp.Testing.Deployment;

// Stands in for the Key Vault key: a CryptographyClient over a JsonWebKey with its private part wraps and unwraps in the
// test process, so the Key Vault extension of Data Protection runs unchanged and no test reaches Azure or needs a credential.
// It answers every key id with its one key, as Key Vault answers the versionless id with the current version.
public sealed class TestKeyEncryptionKeyResolver : IKeyEncryptionKeyResolver
{
    private readonly CryptographyClient _key;

    private int _resolutions;

    public TestKeyEncryptionKeyResolver(string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        using var rsa = RSA.Create(2048);
        _key = new CryptographyClient(new JsonWebKey(rsa, includePrivateParameters: true) { Id = keyId });
    }

    public string KeyId => _key.KeyId;

    public int Resolutions => Volatile.Read(ref _resolutions);

    public static TestKeyEncryptionKeyResolver ForVersionOf(Uri keyIdentifier)
    {
        ArgumentNullException.ThrowIfNull(keyIdentifier);

        return new TestKeyEncryptionKeyResolver($"{keyIdentifier.AbsoluteUri}/{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}");
    }

    public IKeyEncryptionKey Resolve(string keyId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _resolutions);

        return _key;
    }

    public Task<IKeyEncryptionKey> ResolveAsync(string keyId, CancellationToken cancellationToken = default) => Task.FromResult(Resolve(keyId, cancellationToken));
}
