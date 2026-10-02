using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;

internal static class DataProtectionRegistration
{
    private const int KeyVaultMaxRetries = 2;

    private static readonly TimeSpan KeyVaultNetworkTimeout = TimeSpan.FromSeconds(10);

    public static IHostApplicationBuilder AddErpDataProtection(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<KeyRingOptions>()
            .BindConfiguration(KeyRingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<KeyRingOptions>, KeyRingOptionsValidator>());

        var dataProtection = builder.Services.AddDataProtection().SetApplicationName(DataProtectionKeyRing.ApplicationName);

        // The Key Vault extension takes the key identifier when it is registered, so the value is read once, here, like every
        // Erp:Platform:Identity value; KeyRingOptionsValidator then checks the same value when the API starts.
        if (Uri.TryCreate(builder.Configuration[KeyRingOptions.KeyIdentifierKey], UriKind.Absolute, out var keyIdentifier))
        {
            dataProtection
                .PersistKeysToFileSystem(new DirectoryInfo(DataProtectionKeyRing.DefaultDirectory))
                .ProtectKeysWithAzureKeyVault(keyIdentifier, static services => new KeyResolver(services.GetRequiredService<TokenCredential>(), KeyVaultClientOptions()));
        }
        else
        {
            // Only a run that cannot reach the store gets here (tests, CI, the local containers, the store-less API Playwright
            // starts), and KeyRingOptionsValidator refuses it under App Configuration or in Production: no key reaches a disk.
            builder.Services.Configure<KeyManagementOptions>(static options =>
            {
                options.XmlRepository = new InMemoryKeyRepository();
                options.XmlEncryptor = new NullXmlEncryptor();
            });
        }

        builder.Services.AddHostedService<KeyRingStartupCheck>();

        return builder;
    }

    // Data Protection calls Key Vault synchronously and passes no cancellation token, so only these bound an outage.
    private static CryptographyClientOptions KeyVaultClientOptions()
    {
        var options = new CryptographyClientOptions();
        options.Retry.MaxRetries = KeyVaultMaxRetries;
        options.Retry.NetworkTimeout = KeyVaultNetworkTimeout;

        return options;
    }
}
