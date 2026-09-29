using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.UnitTests.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication;

public sealed class EntraSignInOptionsValidatorTests
{
    private const string Section = EntraSignInOptions.SectionName;

    private static readonly DateTimeOffset Now = new(2026, 9, 29, 6, 30, 0, TimeSpan.Zero);

    private static readonly string ValidCertificate = RsaCertificate(Now.AddDays(-1), Now.AddYears(1));

    [Fact]
    public void Validate_CompleteSettings_Succeeds()
    {
        var result = Validator(Environments.Development).Validate(null, ValidOptions());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData(null, "is required")]
    [InlineData(" ", "is required")]
    [InlineData("common", "must be a GUID")]
    [InlineData("organizations", "must be a GUID")]
    [InlineData("consumers", "must be a GUID")]
    [InlineData("contoso.onmicrosoft.com", "must be a GUID")]
    [InlineData("00000000-0000-0000-0000-000000000000", "must be a GUID")]
    public void Validate_TenantIdNotADirectoryId_FailsNamingTheSetting(string? tenantId, string rule)
    {
        var options = ValidOptions();
        options.TenantId = tenantId;

        var result = Validator(Environments.Development).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:TenantId {rule}", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("common, organizations and consumers are refused", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "is required")]
    [InlineData("erp-web", "must be a GUID")]
    [InlineData("00000000-0000-0000-0000-000000000000", "must be a GUID")]
    public void Validate_ClientIdNotAnApplicationId_FailsNamingTheSetting(string? clientId, string rule)
    {
        var options = ValidOptions();
        options.ClientId = clientId;

        var result = Validator(Environments.Development).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:ClientId {rule}", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://login.microsoftonline.com/")]
    [InlineData("https://login.microsoftonline.us")]
    public void Validate_HttpsCloudInstance_Succeeds(string instance)
    {
        var options = ValidOptions();
        options.Instance = instance;

        Assert.True(Validator(Environments.Development).Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("http://login.microsoftonline.com/")]
    [InlineData("https://login.microsoftonline.com/contoso.onmicrosoft.com")]
    [InlineData("https://login.microsoftonline.com/?tenant=contoso")]
    [InlineData("login.microsoftonline.com")]
    [InlineData("")]
    public void Validate_InstanceNotAnHttpsCloudAddress_Fails(string instance)
    {
        var options = ValidOptions();
        options.Instance = instance;

        var result = Validator(Environments.Development).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:Instance must be the https address of the Entra cloud", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development", "http://localhost:3000")]
    [InlineData("Development", "http://127.0.0.1:3000")]
    [InlineData("Development", "http://localhost")]
    [InlineData("Development", "https://erp.example.com")]
    [InlineData("Production", "https://erp.example.com")]
    [InlineData("Production", "https://erp.example.com:8443")]
    public void Validate_WebOriginAllowedInTheEnvironment_Succeeds(string environment, string origin)
    {
        var options = ValidOptions();
        options.WebOrigin = origin;

        var result = Validator(environment).Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData("Production", "http://localhost:3000")]
    [InlineData("Development", "http://erp.example.com")]
    [InlineData("Development", "https://erp.example.com/")]
    [InlineData("Development", "https://erp.example.com/erp")]
    [InlineData("Development", "https://erp.example.com?tenant=1")]
    [InlineData("Development", "https://*.example.com")]
    [InlineData("Development", "https://admin@erp.example.com")]
    [InlineData("Development", "erp.example.com")]
    public void Validate_WebOriginNotAnAllowedOrigin_FailsNamingTheValue(string environment, string origin)
    {
        var options = ValidOptions();
        options.WebOrigin = origin;

        var result = Validator(environment).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:WebOrigin must be an https origin", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains($"'{origin}' was rejected", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WithoutWebOrigin_FailsAsRequired()
    {
        var options = ValidOptions();
        options.WebOrigin = null;

        var result = Validator(Environments.Development).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:WebOrigin is required", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WithoutCertificate_FailsNamingTheKeyVaultSecret()
    {
        var options = ValidOptions();
        options.ClientCertificate = null;

        var result = Validator(Environments.Development).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:ClientCertificate is required", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Erp--Platform--Identity--ClientCertificate", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(UnusableCertificates))]
    public void Validate_UnusableCertificate_FailsWithoutRepeatingTheValue(string certificate, string problem)
    {
        var options = ValidOptions();
        options.ClientCertificate = certificate;

        var result = Validator(Environments.Development).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:ClientCertificate {problem}", result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(certificate, result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_CertificateValidOnlyUntilTheClockPassesIt_FailsOnceExpired()
    {
        var options = ValidOptions();
        var clock = new FakeTimeProvider(Now);
        var validator = new EntraSignInOptionsValidator(clock, new FakeHostEnvironment(Environments.Development));
        Assert.True(validator.Validate(null, options).Succeeded);

        clock.Advance(TimeSpan.FromDays(366));

        Assert.Contains($"{Section}:ClientCertificate is not valid now", validator.Validate(null, options).FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SessionLifetimeShorterThanTheIdleTimeout_Fails()
    {
        var options = ValidOptions();
        options.SessionIdleTimeout = TimeSpan.FromHours(2);
        options.SessionLifetime = TimeSpan.FromHours(1);

        var result = Validator(Environments.Development).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:SessionLifetime must be at least {Section}:SessionIdleTimeout", result.FailureMessage, StringComparison.Ordinal);
    }

    public static TheoryData<string, string> UnusableCertificates() => new()
    {
        { "not base64!", "is not base64 text." },
        { Convert.ToBase64String("not a certificate"u8), "is not a PKCS#12 certificate that opens without a password." },
        { PasswordProtectedCertificate(), "is not a PKCS#12 certificate that opens without a password." },
        { CertificateWithoutKey(), "carries no RSA private key." },
        { EcdsaCertificate(), "carries no RSA private key." },
        { RsaCertificate(Now.AddYears(-2), Now.AddDays(-1)), "is not valid now" },
        { RsaCertificate(Now.AddDays(1), Now.AddYears(1)), "is not valid now" },
    };

    private static EntraSignInOptionsValidator Validator(string environment) =>
        new(new FakeTimeProvider(Now), new FakeHostEnvironment(environment));

    private static EntraSignInOptions ValidOptions() => new()
    {
        TenantId = "5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d",
        ClientId = "0e9d8c7b-6a5f-4e3d-8c1b-0a9f8e7d6c5b",
        WebOrigin = "http://localhost:3000",
        ClientCertificate = ValidCertificate,
    };

    private static string RsaCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        using var certificate = new CertificateRequest("CN=erp-sign-in-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(notBefore, notAfter);

        return Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));
    }

    private static string PasswordProtectedCertificate()
    {
        using var key = RSA.Create(2048);
        using var certificate = new CertificateRequest("CN=erp-sign-in-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(Now.AddDays(-1), Now.AddYears(1));

        return Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12, "not-the-empty-password"));
    }

    private static string CertificateWithoutKey()
    {
        using var key = RSA.Create(2048);
        using var withKey = new CertificateRequest("CN=erp-sign-in-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(Now.AddDays(-1), Now.AddYears(1));
        using var publicOnly = X509CertificateLoader.LoadCertificate(withKey.RawData);

        return Convert.ToBase64String(publicOnly.Export(X509ContentType.Pkcs12));
    }

    private static string EcdsaCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = new CertificateRequest("CN=erp-sign-in-test", key, HashAlgorithmName.SHA256).CreateSelfSigned(Now.AddDays(-1), Now.AddYears(1));

        return Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));
    }
}
