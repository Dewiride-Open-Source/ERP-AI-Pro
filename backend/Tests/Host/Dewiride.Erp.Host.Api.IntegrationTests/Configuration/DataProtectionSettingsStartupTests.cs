using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Deployment;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Configuration;

public sealed class DataProtectionSettingsStartupTests
{
    [Fact]
    public void Start_InProductionWithoutAKeyIdentifier_FailsNamingTheSetting()
    {
        using var factory = ErpApiFactory.ForEnvironment(Environments.Production).WithConfiguration(ErpApiFactory.DataProtectionKeyIdentifierKey, string.Empty);

        var failure = Assert.Throws<OptionsValidationException>(() => factory.Services);

        Assert.Equal(typeof(KeyRingOptions), failure.OptionsType);
        Assert.Contains($"{ErpApiFactory.DataProtectionKeyIdentifierKey} is required", failure.Message, StringComparison.Ordinal);
        Assert.Contains("https://<vault>.vault.azure.net/keys/<name>", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_InProductionWithAKeyIdentifier_Starts()
    {
        using var factory = ErpApiFactory.ForEnvironment(Environments.Production);

        var options = factory.Services.GetRequiredService<IOptions<KeyRingOptions>>().Value;

        Assert.Equal(factory.Deployment.KeyIdentifier, options.KeyIdentifier);
    }

    [Theory]
    [InlineData("https://erp-test.vault.azure.net/keys/data-protection/0123456789abcdef0123456789abcdef", "must be versionless")]
    [InlineData("http://erp-test.vault.azure.net/keys/data-protection", "must be the identifier of a Key Vault key")]
    [InlineData("https://erp-test.example.com/keys/data-protection", "must be the identifier of a Key Vault key")]
    [InlineData("https://erp-test.vault.azure.net/secrets/data-protection", "must name a key with the path /keys/<name>")]
    [InlineData("erp-test.vault.azure.net/keys/data-protection", "must be the identifier of a Key Vault key")]
    public void Start_WithAKeyIdentifierThatIsNotAVersionlessKeyVaultKey_FailsNamingTheSetting(string keyIdentifier, string rule)
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        using var factory = new ErpApiFactory().WithDeployment(deployment).WithConfiguration(ErpApiFactory.DataProtectionKeyIdentifierKey, keyIdentifier);

        var failure = Assert.Throws<OptionsValidationException>(() => factory.Services);

        Assert.Equal(typeof(KeyRingOptions), failure.OptionsType);
        Assert.Contains($"{ErpApiFactory.DataProtectionKeyIdentifierKey} {rule}", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(keyIdentifier, failure.Message, StringComparison.Ordinal);
    }
}
