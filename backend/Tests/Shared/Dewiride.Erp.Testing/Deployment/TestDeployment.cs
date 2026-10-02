using System.Security.Cryptography;
using Azure.Core.Cryptography;
using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.Testing.Deployment;

// One deployment of the API: its hosts share token cache entries, sign-out records and, with a persisted key ring, Data
// Protection keys, as the instances of one installation do. The personas sign in with the same account ids on every host of the
// test process, which shares one database, so each deployment keeps every row of the keyed SQL Server cache under a key prefix
// of its own; otherwise tests running in parallel would sign each other out. A persisted key ring lives in a temporary
// directory, deleted on dispose, and is wrapped by a key that never leaves the process, so no test touches Azure or the key
// folder of the person running it.
public sealed class TestDeployment : IDisposable
{
    public const string KeyVaultOrigin = "https://erp-test.vault.azure.net";

    private const string KeyDirectoryPrefix = "erp-data-protection-keys-";

    private static readonly TimeSpan LeftoverAge = TimeSpan.FromHours(24);

    // A test process that crashed leaves its directories behind; the first persisted deployment of the next process removes
    // those older than a day, as SqlTestDatabase does for its databases.
    private static readonly Lazy<bool> LeftoversRemoved = new(RemoveLeftoverKeyDirectories);

    private TestDeployment(bool persistedKeyRing)
    {
        var name = $"test-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))}";
        TokenCacheKeyPrefix = $"{name}:";
        if (persistedKeyRing)
        {
            _ = LeftoversRemoved.Value;
            KeyIdentifier = new Uri($"{KeyVaultOrigin}/keys/{name}");
            KeyDirectory = Directory.CreateTempSubdirectory(KeyDirectoryPrefix);
            KeyResolver = TestKeyEncryptionKeyResolver.ForVersionOf(KeyIdentifier);
        }
    }

    public string TokenCacheKeyPrefix { get; }

    public Uri? KeyIdentifier { get; }

    public DirectoryInfo? KeyDirectory { get; }

    public TestKeyEncryptionKeyResolver? KeyResolver { get; }

    public static TestDeployment WithInMemoryKeyRing() => new(persistedKeyRing: false);

    public static TestDeployment WithPersistedKeyRing() => new(persistedKeyRing: true);

    public string TokenCacheKeyFor(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return TokenCacheKeyPrefix + user.AccountId;
    }

    public IReadOnlyList<FileInfo> KeyFiles() =>
        KeyDirectory is null ? [] : [.. KeyDirectory.EnumerateFiles("key-*.xml").OrderBy(file => file.Name, StringComparer.Ordinal)];

    public void Dispose()
    {
        KeyDirectory?.Refresh();
        if (KeyDirectory is { Exists: true })
        {
            KeyDirectory.Delete(recursive: true);
        }
    }

    // A KeyIdentifier the API was given makes it register the Key Vault resolver; a deployment without a persisted key ring has
    // no directory or key to put in their place, so the host must not start with the user's key folder and an Azure credential.
    internal void Register(IServiceCollection services)
    {
        services.DecorateTokenCacheStore(store => new PrefixedDistributedCache(TokenCacheKeyPrefix, store));
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IKeyEncryptionKeyResolver)))
        {
            return;
        }

        if (KeyDirectory is null || KeyResolver is null)
        {
            throw new InvalidOperationException(
                $"{KeyRingOptions.KeyIdentifierKey} is set, so the API protects its key ring with Key Vault in {DataProtectionKeyRing.DefaultDirectory}; " +
                "give the factory a deployment with a persisted key ring: ErpApiFactory.WithDeployment(TestDeployment.WithPersistedKeyRing()).");
        }

        var directory = KeyDirectory;
        services.AddOptions<KeyManagementOptions>().Configure<ILoggerFactory>((options, loggers) => options.XmlRepository = new FileSystemXmlRepository(directory, loggers));
        services.AddSingleton<IKeyEncryptionKeyResolver>(KeyResolver);
    }

    private static bool RemoveLeftoverKeyDirectories()
    {
        var cutoff = TimeProvider.System.GetUtcNow().UtcDateTime - LeftoverAge;
        foreach (var directory in new DirectoryInfo(Path.GetTempPath()).EnumerateDirectories(KeyDirectoryPrefix + "*"))
        {
            if (directory.CreationTimeUtc >= cutoff)
            {
                continue;
            }

            try
            {
                directory.Delete(recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A directory another test process still holds is left for a later run.
            }
        }

        return true;
    }
}
