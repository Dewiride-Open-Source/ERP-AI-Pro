using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.Testing;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Configuration;

public sealed class IdentitySettingsStartupTests
{
    [Theory]
    [InlineData(ErpApiFactory.IdentityTenantIdKey, "", "is required")]
    [InlineData(ErpApiFactory.IdentityTenantIdKey, "organizations", "must be a GUID")]
    [InlineData(ErpApiFactory.IdentityTenantIdKey, "{5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d}", "must be a GUID")]
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

    [Theory]
    [InlineData(ErpApiFactory.IdentitySessionIdleTimeoutKey, "00:00:59", nameof(EntraSignInOptions.SessionIdleTimeout))]
    [InlineData(ErpApiFactory.IdentitySessionIdleTimeoutKey, "1.00:00:01", nameof(EntraSignInOptions.SessionIdleTimeout))]
    [InlineData(ErpApiFactory.IdentitySessionLifetimeKey, "00:00:59", nameof(EntraSignInOptions.SessionLifetime))]
    [InlineData(ErpApiFactory.IdentitySessionLifetimeKey, "7.00:00:01", nameof(EntraSignInOptions.SessionLifetime))]
    public void Start_WithASessionDurationOutsideItsRange_FailsNamingTheSetting(string key, string value, string member)
    {
        using var factory = new ErpApiFactory().WithConfiguration(key, value);

        var failure = Assert.Throws<OptionsValidationException>(() => factory.Services);

        Assert.Equal(typeof(EntraSignInOptions), failure.OptionsType);
        Assert.Contains($"The field {member} must be between", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("00:01:00", "00:01:00")]
    [InlineData("1.00:00:00", "7.00:00:00")]
    public void Start_WithSessionDurationsAtTheLimitsOfTheirRanges_Starts(string idleTimeout, string lifetime)
    {
        using var factory = new ErpApiFactory()
            .WithConfiguration(ErpApiFactory.IdentitySessionIdleTimeoutKey, idleTimeout)
            .WithConfiguration(ErpApiFactory.IdentitySessionLifetimeKey, lifetime);

        var options = factory.Services.GetRequiredService<IOptions<EntraSignInOptions>>().Value;

        Assert.Equal(TimeSpan.Parse(idleTimeout, CultureInfo.InvariantCulture), options.SessionIdleTimeout);
        Assert.Equal(TimeSpan.Parse(lifetime, CultureInfo.InvariantCulture), options.SessionLifetime);
    }
}
