using Azure.Core;
using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.DataProtection;

// Every API test host replaces the key folder with a temporary one, so only this test sees the folder the product uses. The
// services are built but never started, so nothing creates the folder, writes a key or reaches Key Vault.
public sealed class DataProtectionRegistrationTests
{
    private const string KeyIdentifier = "https://erp-test.vault.azure.net/keys/data-protection";

    [Fact]
    public void AddErpDataProtection_WithAKeyIdentifier_KeepsTheKeysWrappedByKeyVaultInTheProductFolderWithoutTouchingIt()
    {
        var folderExisted = Directory.Exists(DataProtectionKeyRing.DefaultDirectory);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [KeyRingOptions.KeyIdentifierKey] = KeyIdentifier });
        builder.Services.AddSingleton<TokenCredential>(new UnusedCredential());
        builder.AddErpDataProtection();
        using var provider = builder.Services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        var repository = Assert.IsType<FileSystemXmlRepository>(options.XmlRepository);
        Assert.Equal(DataProtectionKeyRing.DefaultDirectory, repository.Directory.FullName);
        Assert.Equal("Azure.Extensions.AspNetCore.DataProtection.Keys.AzureKeyVaultXmlEncryptor", options.XmlEncryptor?.GetType().FullName);
        Assert.Equal(DataProtectionKeyRing.ApplicationName, provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
        Assert.Equal(folderExisted, Directory.Exists(DataProtectionKeyRing.DefaultDirectory));
    }

    private sealed class UnusedCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The test reached Key Vault.");

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The test reached Key Vault.");
    }
}
