using System.Xml.Linq;
using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Deployment;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.IntegrationTests.DataProtection;

public sealed class KeyRingStorageTests
{
    private const string KeyVaultDecryptorType = "Azure.Extensions.AspNetCore.DataProtection.Keys.AzureKeyVaultXmlDecryptor";

    private const string DataProtectionNamespace = "http://schemas.asp.net/2015/03/dataProtection";

    [Fact]
    public async Task Start_WithAPersistedKeyRing_WritesItsKeysWrappedByTheVersionedKeyIntoTheDeploymentDirectory()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        await using var factory = new ErpApiFactory().WithDeployment(deployment);

        var options = factory.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        var repository = Assert.IsType<FileSystemXmlRepository>(options.XmlRepository);
        Assert.Equal(deployment.KeyDirectory!.FullName, repository.Directory.FullName);
        var file = Assert.Single(deployment.KeyFiles());
        var key = XDocument.Load(file.FullName);
        var secret = Assert.Single(key.Descendants(XName.Get("encryptedSecret", DataProtectionNamespace)));
        Assert.StartsWith($"{KeyVaultDecryptorType},", secret.Attribute("decryptorType")?.Value, StringComparison.Ordinal);
        var kid = Assert.Single(secret.Descendants("kid")).Value;
        Assert.Equal(deployment.KeyResolver!.KeyId, kid);
        Assert.StartsWith($"{deployment.KeyIdentifier!.AbsoluteUri}/", kid, StringComparison.Ordinal);
        Assert.DoesNotContain(key.Descendants(), element => element.Name.LocalName == "masterKey");
    }

    [Fact]
    public async Task Start_WithoutAKeyIdentifier_KeepsTheKeysInMemoryUnderTheFixedApplicationName()
    {
        await using var factory = new ErpApiFactory();

        var options = factory.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        var repository = Assert.IsType<InMemoryKeyRepository>(options.XmlRepository);
        Assert.NotEmpty(repository.GetAllElements());
        Assert.IsType<NullXmlEncryptor>(options.XmlEncryptor);
        Assert.Equal(DataProtectionKeyRing.ApplicationName, factory.Services.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
    }

    [Fact]
    public async Task Start_AgainOnAPersistedKeyRing_ReusesTheKeyInsteadOfCreatingAnother()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        await using (var first = new ErpApiFactory().WithDeployment(deployment))
        {
            _ = first.Services;
        }

        await using var second = new ErpApiFactory().WithDeployment(deployment);
        _ = second.Services;

        Assert.Single(deployment.KeyFiles());
    }
}
