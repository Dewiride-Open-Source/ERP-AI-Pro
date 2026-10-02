using System.Security.Cryptography;
using System.Xml.Linq;
using Azure;
using Azure.Core.Cryptography;
using Azure.Security.KeyVault.Keys.Cryptography;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;

// Data Protection never fails on its own: its hosted service logs a key ring that cannot load at Information and carries on,
// and when an existing key cannot be unwrapped it silently creates a new one, which signs everyone out. The host runs every
// StartingAsync before any StartAsync, so these checks run first, and a failure stops the API before it serves a request.
internal sealed partial class KeyRingStartupCheck(
    IOptions<KeyManagementOptions> keyManagement,
    IOptions<KeyRingOptions> keyRing,
    IOptions<EntraSignInOptions> signIn,
    IKeyManager keyManager,
    IDataProtectionProvider dataProtection,
    TimeProvider timeProvider,
    ILogger<KeyRingStartupCheck> logger,
    IKeyEncryptionKeyResolver? keyEncryptionKeyResolver = null) : IHostedLifecycleService
{
    public const string ProbePurpose = "Dewiride.Erp.Authentication.KeyRingCheck";

    private const string KeyVaultDecryptorType = "Azure.Extensions.AspNetCore.DataProtection.Keys.AzureKeyVaultXmlDecryptor";

    private const string MasterKeyElement = "masterKey";

    private const int ProbeSecretLength = 16;

    private const int ProbePayloadLength = 32;

    private static readonly XNamespace DataProtectionNamespace = "http://schemas.asp.net/2015/03/dataProtection";

    private static readonly XName KeyElement = "key";

    private static readonly XName EncryptedSecretElement = DataProtectionNamespace + "encryptedSecret";

    private static readonly XName DecryptorTypeAttribute = "decryptorType";

    private static readonly XName IdAttribute = "id";

    private static readonly string WrapAlgorithm = KeyWrapAlgorithm.RsaOaep.ToString();

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var directory = (keyManagement.Value.XmlRepository as FileSystemXmlRepository)?.Directory;
            if (directory is not null)
            {
                await KeyDirectoryProbe.ForThisSystem.ProbeAsync(directory, cancellationToken).ConfigureAwait(false);
            }

            if (keyEncryptionKeyResolver is not null)
            {
                await CheckKeyEncryptionKeyAsync(keyEncryptionKeyResolver, cancellationToken).ConfigureAwait(false);
                RefuseKeysNotWrappedByKeyVault(keyManagement.Value.XmlRepository?.GetAllElements() ?? []);
            }

            DecryptKeysThatMayProtectLiveData();
            RoundTripAProbe();

            var keyCount = keyManager.GetAllKeys().Count;
            if (directory is null)
            {
                LogReadyInMemory(logger, keyCount);
            }
            else
            {
                LogReadyInDirectory(logger, keyCount, directory.FullName);
            }
        }
        catch (Exception exception)
        {
            LogFailure(exception);
            throw;
        }
    }

    public static string UnwrappedKeyMessage(string file) =>
        $"The Data Protection key ring holds a key that the Key Vault key named by {KeyRingOptions.KeyIdentifierKey} does not wrap: {file}. " +
        "This API never stores such a key, so the file was put in the key folder by something else; remove it once its origin is known.";

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public static bool IsWrappedByKeyVault(XElement key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var secrets = key.Descendants(EncryptedSecretElement).ToList();

        // Data Protection reads the first master key of a descriptor once its secrets are decrypted, so one in the clear
        // beside a Key Vault envelope would be the key in use.
        return secrets.Count > 0
            && secrets.TrueForAll(secret => string.Equals(TypeNameOf((string?)secret.Attribute(DecryptorTypeAttribute)), KeyVaultDecryptorType, StringComparison.Ordinal))
            && !key.Descendants().Any(element => element.Name.LocalName == MasterKeyElement);
    }

    // Without keys/read the resolver hands back the versionless identifier, which each new key would record: after the next
    // rotation that identifier names a version that never wrapped it.
    private async Task CheckKeyEncryptionKeyAsync(IKeyEncryptionKeyResolver resolver, CancellationToken cancellationToken)
    {
        var versionless = (keyRing.Value.KeyIdentifier ?? throw new InvalidOperationException($"{KeyRingOptions.KeyIdentifierKey} is required to protect the key ring with Key Vault.")).AbsoluteUri;
        var current = await resolver.ResolveAsync(versionless, cancellationToken).ConfigureAwait(false);
        if (string.Equals(current.KeyId, versionless, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The Key Vault key named by {KeyRingOptions.KeyIdentifierKey} resolved without a version: the API identity may not read the key (keys/read), " +
                "so each new Data Protection key would record the versionless identifier and become unreadable once the key is rotated.");
        }

        var secret = RandomNumberGenerator.GetBytes(ProbeSecretLength);
        var wrapped = await current.WrapKeyAsync(WrapAlgorithm, secret, cancellationToken).ConfigureAwait(false);
        var unwrapping = await resolver.ResolveAsync(current.KeyId, cancellationToken).ConfigureAwait(false);
        var unwrapped = await unwrapping.UnwrapKeyAsync(WrapAlgorithm, wrapped, cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(secret, unwrapped))
        {
            throw new CryptographicException($"The Key Vault key named by {KeyRingOptions.KeyIdentifierKey} did not unwrap the secret it wrapped.");
        }
    }

    private static void RefuseKeysNotWrappedByKeyVault(IReadOnlyCollection<XElement> elements)
    {
        // A key that Key Vault does not wrap could become the default key, or open a cookie forged under a secret its author
        // knows, whether it is current or expired. The failure names the key by its id only, never by its content.
        var unwrapped = elements.FirstOrDefault(element => element.Name == KeyElement && !IsWrappedByKeyVault(element));
        if (unwrapped is not null)
        {
            var file = Guid.TryParse((string?)unwrapped.Attribute(IdAttribute), out var id) ? $"key-{id:D}.xml" : "a file that names no valid key id";
            throw new InvalidOperationException(UnwrappedKeyMessage(file));
        }
    }

    // Reading the descriptor decrypts the key, so a key that cannot be decrypted fails here instead of being passed over for a
    // new one. A key that expired up to a session lifetime and the token cache margin ago may still protect a cookie or an entry.
    private void DecryptKeysThatMayProtectLiveData()
    {
        var horizon = timeProvider.GetUtcNow() - signIn.Value.SessionLifetime - TokenCacheRegistration.ExpirationMargin;
        foreach (var key in keyManager.GetAllKeys())
        {
            if (!key.IsRevoked && key.ExpirationDate >= horizon)
            {
                _ = key.Descriptor;
            }
        }
    }

    // Creates the first key when the ring is empty.
    private void RoundTripAProbe()
    {
        var protector = dataProtection.CreateProtector(ProbePurpose);
        var payload = RandomNumberGenerator.GetBytes(ProbePayloadLength);
        if (!CryptographicOperations.FixedTimeEquals(payload, protector.Unprotect(protector.Protect(payload))))
        {
            throw new CryptographicException("The Data Protection key ring did not return the payload it protected.");
        }
    }

    // Data Protection wraps a Key Vault refusal in a CryptographicException, and Azure.Core wraps the attempts of a retried
    // request in an AggregateException.
    private void LogFailure(Exception exception)
    {
        var exceptionType = exception.GetType().FullName;
        var refused = RequestFailureIn(exception);
        if (refused is null)
        {
            LogFailed(logger, exception, exceptionType);
        }
        else
        {
            LogKeyVaultRefused(logger, exception, exceptionType, refused.Status, refused.ErrorCode);
        }
    }

    private static RequestFailedException? RequestFailureIn(Exception exception)
    {
        for (var inner = exception; inner is not null; inner = inner.InnerException)
        {
            if (inner is RequestFailedException refused)
            {
                return refused;
            }
        }

        return null;
    }

    private static string? TypeNameOf(string? assemblyQualifiedName) => assemblyQualifiedName?.Split(',', 2)[0].Trim();

    [LoggerMessage(Level = LogLevel.Information, Message = "The Data Protection key ring is ready with {KeyCount} keys in {Directory}")]
    private static partial void LogReadyInDirectory(ILogger logger, int keyCount, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "The Data Protection key ring is ready with {KeyCount} keys held in memory only, so a restart signs everyone out")]
    private static partial void LogReadyInMemory(ILogger logger, int keyCount);

    [LoggerMessage(Level = LogLevel.Critical, Message = "The Data Protection key ring failed its startup check with {ExceptionType}: Key Vault answered {Status} with error code {ErrorCode}; the API does not start")]
    private static partial void LogKeyVaultRefused(ILogger logger, Exception exception, string? exceptionType, int status, string? errorCode);

    [LoggerMessage(Level = LogLevel.Critical, Message = "The Data Protection key ring failed its startup check with {ExceptionType}; the API does not start")]
    private static partial void LogFailed(ILogger logger, Exception exception, string? exceptionType);
}
