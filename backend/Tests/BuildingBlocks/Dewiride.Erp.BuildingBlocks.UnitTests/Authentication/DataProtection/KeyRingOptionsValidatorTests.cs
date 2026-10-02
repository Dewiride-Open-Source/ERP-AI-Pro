using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.UnitTests.Configuration;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.DataProtection;

public sealed class KeyRingOptionsValidatorTests
{
    private const string Setting = KeyRingOptions.KeyIdentifierKey;

    [Theory]
    [InlineData("https://erp-kv-dev.vault.azure.net/keys/Erp--Platform--DataProtection--Key")]
    [InlineData("https://abc.vault.azure.net/keys/k")]
    [InlineData("https://a23456789012345678901234.vault.azure.net/keys/data-protection")]
    [InlineData("https://ERP-KV-DEV.VAULT.AZURE.NET/keys/Key-1")]
    [InlineData("https://erp-kv-dev.vault.azure.net:443/keys/data-protection")]
    public void Validate_VersionlessKeyVaultKey_Succeeds(string keyIdentifier)
    {
        var result = Validator(ErpConfigurationSource.AppConfiguration, Environments.Production).Validate(null, Options(keyIdentifier));

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void Validate_KeyNameOf127Characters_Succeeds()
    {
        var result = Validator(ErpConfigurationSource.AppConfiguration, Environments.Production).Validate(null, Options($"https://erp-kv.vault.azure.net/keys/{new string('k', 127)}"));

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData(ErpConfigurationSource.InMemory, "Development")]
    [InlineData(ErpConfigurationSource.LocalDevelopment, "Development")]
    [InlineData(ErpConfigurationSource.InMemory, "Staging")]
    public void Validate_WithoutAKeyIdentifierOutsideTheStoreAndProduction_Succeeds(ErpConfigurationSource source, string environment)
    {
        var result = Validator(source, environment).Validate(null, new KeyRingOptions());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData(ErpConfigurationSource.AppConfiguration, "Development", null)]
    [InlineData(ErpConfigurationSource.AppConfiguration, "Development", "")]
    [InlineData(ErpConfigurationSource.AppConfiguration, "Development", "  ")]
    [InlineData(ErpConfigurationSource.InMemory, "Production", null)]
    [InlineData(ErpConfigurationSource.LocalDevelopment, "Production", "")]
    public void Validate_WithoutAKeyIdentifierUnderTheStoreOrInProduction_FailsNamingTheSettingAndItsShape(ErpConfigurationSource source, string environment, string? keyIdentifier)
    {
        var options = keyIdentifier is null ? new KeyRingOptions() : Options(keyIdentifier);

        var result = Validator(source, environment).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"{Setting} is required", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("https://<vault>.vault.azure.net/keys/<name>", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://erp-kv-dev.vault.azure.net/keys/data-protection/0123456789abcdef0123456789abcdef")]
    [InlineData("https://erp-kv-dev.vault.azure.net/keys/data-protection/latest")]
    public void Validate_VersionedKeyIdentifier_FailsAskingForTheVersionlessOne(string keyIdentifier)
    {
        var result = Validator(ErpConfigurationSource.InMemory, Environments.Development).Validate(null, Options(keyIdentifier));

        Assert.True(result.Failed);
        Assert.Contains($"{Setting} must be versionless", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("a rotated key keeps working", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://erp-kv-dev.vault.azure.net/keys/data-protection")]
    [InlineData("https://erp-kv-dev.vault.azure.net:8443/keys/data-protection")]
    [InlineData("https://admin:secret@erp-kv-dev.vault.azure.net/keys/data-protection")]
    [InlineData("https://erp-kv-dev.vault.azure.net/keys/data-protection?api-version=7.4")]
    [InlineData("https://erp-kv-dev.vault.azure.net/keys/data-protection#current")]
    [InlineData("https://erp-kv-dev.vault.azure.com/keys/data-protection")]
    [InlineData("https://erp-kv-dev.managedhsm.azure.net/keys/data-protection")]
    [InlineData("https://kv.vault.azure.net/keys/data-protection")]
    [InlineData("https://a234567890123456789012345.vault.azure.net/keys/data-protection")]
    [InlineData("https://erp_kv.vault.azure.net/keys/data-protection")]
    [InlineData("https://vault.azure.net/keys/data-protection")]
    [InlineData("erp-kv-dev.vault.azure.net/keys/data-protection")]
    [InlineData("/keys/data-protection")]
    public void Validate_NotAKeyVaultKeyAddress_FailsWithoutEchoingTheValue(string keyIdentifier)
    {
        var result = Validator(ErpConfigurationSource.InMemory, Environments.Development).Validate(null, Options(keyIdentifier));

        Assert.True(result.Failed);
        Assert.Contains($"{Setting} must be the identifier of a Key Vault key", result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(keyIdentifier, result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://erp-kv-dev.vault.azure.net/")]
    [InlineData("https://erp-kv-dev.vault.azure.net/keys/")]
    [InlineData("https://erp-kv-dev.vault.azure.net/keys/data-protection/")]
    [InlineData("https://erp-kv-dev.vault.azure.net/secrets/data-protection")]
    [InlineData("https://erp-kv-dev.vault.azure.net/certificates/data-protection")]
    [InlineData("https://erp-kv-dev.vault.azure.net/keys/data_protection")]
    [InlineData("https://erp-kv-dev.vault.azure.net/keys/data%20protection")]
    [InlineData("https://erp-kv-dev.vault.azure.net/keys/data-protection/0123/extra")]
    public void Validate_PathOtherThanAKeyName_FailsNamingTheShape(string keyIdentifier)
    {
        var result = Validator(ErpConfigurationSource.InMemory, Environments.Development).Validate(null, Options(keyIdentifier));

        Assert.True(result.Failed);
        Assert.Contains($"{Setting} must name a key with the path /keys/<name>", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_KeyNameLongerThan127Characters_Fails()
    {
        var result = Validator(ErpConfigurationSource.InMemory, Environments.Development).Validate(null, Options($"https://erp-kv.vault.azure.net/keys/{new string('k', 128)}"));

        Assert.True(result.Failed);
        Assert.Contains($"{Setting} must name a key with the path /keys/<name>", result.FailureMessage, StringComparison.Ordinal);
    }

    private static KeyRingOptions Options(string keyIdentifier) => new() { KeyIdentifier = new Uri(keyIdentifier, UriKind.RelativeOrAbsolute) };

    private static KeyRingOptionsValidator Validator(ErpConfigurationSource source, string environment) =>
        new(new ErpConfigurationInfo(source, Label: null, Endpoint: null), new FakeHostEnvironment(environment));
}
