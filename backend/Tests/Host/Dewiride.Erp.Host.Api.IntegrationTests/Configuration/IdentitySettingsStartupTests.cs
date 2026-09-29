using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.Testing;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Configuration;

public sealed class IdentitySettingsStartupTests
{
    [Theory]
    [InlineData(ErpApiFactory.IdentityTenantIdKey, "", "is required")]
    [InlineData(ErpApiFactory.IdentityTenantIdKey, "organizations", "must be a GUID")]
    [InlineData(ErpApiFactory.IdentityClientIdKey, "", "is required")]
    [InlineData(ErpApiFactory.IdentityClientIdKey, "erp-web", "must be a GUID")]
    [InlineData(ErpApiFactory.IdentityWebOriginKey, "", "is required")]
    [InlineData(ErpApiFactory.IdentityWebOriginKey, "http://erp.example.com", "must be an https origin")]
    [InlineData(ErpApiFactory.IdentityClientCertificateKey, "", "is required")]
    [InlineData(ErpApiFactory.IdentityClientCertificateKey, "bm90IGEgY2VydGlmaWNhdGU=", "is not a PKCS#12 certificate")]
    public void Start_WithAMissingOrInvalidIdentitySetting_FailsNamingTheSetting(string key, string value, string rule)
    {
        using var factory = new ErpApiFactory().WithConfiguration(key, value);

        var failure = Assert.Throws<OptionsValidationException>(() => factory.Services);

        Assert.Equal(typeof(EntraSignInOptions), failure.OptionsType);
        Assert.Contains($"{key} {rule}", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_WithAnHttpLocalWebOriginInProduction_FailsNamingTheSetting()
    {
        using var factory = ErpApiFactory.ForEnvironment(Environments.Production).WithConfiguration(ErpApiFactory.IdentityWebOriginKey, "http://localhost:3000");

        var failure = Assert.Throws<OptionsValidationException>(() => factory.Services);

        Assert.Contains($"{ErpApiFactory.IdentityWebOriginKey} must be an https origin", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_WithAnIdleTimeoutUnderAMinute_FailsTheRange()
    {
        using var factory = new ErpApiFactory().WithConfiguration(ErpApiFactory.IdentitySessionIdleTimeoutKey, "00:00:30");

        var failure = Assert.Throws<OptionsValidationException>(() => factory.Services);

        Assert.Contains(nameof(EntraSignInOptions.SessionIdleTimeout), failure.Message, StringComparison.Ordinal);
    }
}
