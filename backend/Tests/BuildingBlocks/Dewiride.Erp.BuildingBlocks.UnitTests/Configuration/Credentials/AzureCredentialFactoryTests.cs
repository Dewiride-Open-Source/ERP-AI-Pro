using Azure.Identity;
using Dewiride.Erp.BuildingBlocks.Configuration.Credentials;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.Credentials;

public sealed class AzureCredentialFactoryTests
{
    private const string TenantVariable = "AZURE_TENANT_ID";
    private const string ClientVariable = "AZURE_CLIENT_ID";
    private const string CertificatePathVariable = "AZURE_CLIENT_CERTIFICATE_PATH";
    private const string ClientSecretVariable = "AZURE_CLIENT_SECRET";

    [Fact]
    public void Create_DevelopmentWithAzureCliSelection_ReturnsDefaultAzureCredential()
    {
        var variables = Variables((AzureCredentialFactory.SelectionVariable, AzureCredentialFactory.DevelopmentSelection));

        var credential = AzureCredentialFactory.Create(new FakeHostEnvironment(Environments.Development), variables);

        Assert.IsType<DefaultAzureCredential>(credential);
    }

    [Fact]
    public void Create_WithoutSelection_ThrowsNamingAzureTokenCredentials()
    {
        var variables = Variables();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AzureCredentialFactory.Create(new FakeHostEnvironment(Environments.Development), variables));

        Assert.Contains(AzureCredentialFactory.SelectionVariable, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".pem")]
    [InlineData(".pfx")]
    public void Create_ProductionWithEnvironmentCredentialAndCertificateFile_ReturnsDefaultAzureCredential(string extension)
    {
        using var certificate = new TemporaryCertificateFile(extension);
        var variables = ProductionVariables(certificate.FullName);

        var credential = AzureCredentialFactory.Create(new FakeHostEnvironment(Environments.Production), Lookup(variables));

        Assert.IsType<DefaultAzureCredential>(credential);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Create_OutsideDevelopmentWithAzureCliSelection_ThrowsRequiringEnvironmentCredential(string environmentName)
    {
        var variables = ProductionVariables("/run/secrets/erp-runtime-client.pem");
        variables[AzureCredentialFactory.SelectionVariable] = AzureCredentialFactory.DevelopmentSelection;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AzureCredentialFactory.Create(new FakeHostEnvironment(environmentName), Lookup(variables)));

        Assert.Contains(AzureCredentialFactory.SelectionVariable, exception.Message, StringComparison.Ordinal);
        Assert.Contains(AzureCredentialFactory.RuntimeSelection, exception.Message, StringComparison.Ordinal);
        Assert.Contains(environmentName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ProductionWithoutTenantId_ThrowsNamingAzureTenantId()
    {
        var variables = ProductionVariables("/run/secrets/erp-runtime-client.pem");
        variables[TenantVariable] = null;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AzureCredentialFactory.Create(new FakeHostEnvironment(Environments.Production), Lookup(variables)));

        Assert.Contains(TenantVariable, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ProductionWithoutClientId_ThrowsNamingAzureClientId()
    {
        var variables = ProductionVariables("/run/secrets/erp-runtime-client.pem");
        variables[ClientVariable] = string.Empty;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AzureCredentialFactory.Create(new FakeHostEnvironment(Environments.Production), Lookup(variables)));

        Assert.Contains(ClientVariable, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ProductionWithoutCertificatePath_ThrowsNamingAzureClientCertificatePath()
    {
        var variables = ProductionVariables(certificatePath: null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AzureCredentialFactory.Create(new FakeHostEnvironment(Environments.Production), Lookup(variables)));

        Assert.Contains(CertificatePathVariable, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ProductionWithMissingCertificateFile_ThrowsNamingAzureClientCertificatePath()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Path.GetRandomFileName(), ".pem"));
        var variables = ProductionVariables(missingPath);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AzureCredentialFactory.Create(new FakeHostEnvironment(Environments.Production), Lookup(variables)));

        Assert.Contains(CertificatePathVariable, exception.Message, StringComparison.Ordinal);
        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    [Fact]
    public void Create_ProductionWithUnreadableCertificatePath_ThrowsNamingAzureClientCertificatePath()
    {
        var root = Directory.CreateTempSubdirectory("erp-secrets-").FullName;
        var directoryNamedLikeACertificate = Directory.CreateDirectory(Path.Combine(root, "erp-runtime-client.pem")).FullName;
        try
        {
            var variables = ProductionVariables(directoryNamedLikeACertificate);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                AzureCredentialFactory.Create(new FakeHostEnvironment(Environments.Production), Lookup(variables)));

            Assert.Contains(CertificatePathVariable, exception.Message, StringComparison.Ordinal);
            Assert.Contains("cannot be opened for reading", exception.Message, StringComparison.Ordinal);
            Assert.NotNull(exception.InnerException);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Create_ProductionWithUnsupportedCertificateExtension_ThrowsNamingAzureClientCertificatePath()
    {
        using var certificate = new TemporaryCertificateFile(".txt");
        var variables = ProductionVariables(certificate.FullName);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AzureCredentialFactory.Create(new FakeHostEnvironment(Environments.Production), Lookup(variables)));

        Assert.Contains(CertificatePathVariable, exception.Message, StringComparison.Ordinal);
        Assert.Contains(".pem", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ProductionWithClientSecret_ThrowsRejectingAzureClientSecret()
    {
        using var certificate = new TemporaryCertificateFile(".pem");
        var variables = ProductionVariables(certificate.FullName);
        variables[ClientSecretVariable] = "not-allowed";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AzureCredentialFactory.Create(new FakeHostEnvironment(Environments.Production), Lookup(variables)));

        Assert.Contains(ClientSecretVariable, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not-allowed", exception.Message, StringComparison.Ordinal);
    }

    private static Dictionary<string, string?> ProductionVariables(string? certificatePath) => new(StringComparer.Ordinal)
    {
        [AzureCredentialFactory.SelectionVariable] = AzureCredentialFactory.RuntimeSelection,
        [TenantVariable] = "00000000-0000-0000-0000-000000000001",
        [ClientVariable] = "00000000-0000-0000-0000-000000000002",
        [CertificatePathVariable] = certificatePath,
    };

    private static Func<string, string?> Variables(params (string Name, string? Value)[] values) =>
        Lookup(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    private static Func<string, string?> Lookup(Dictionary<string, string?> variables) =>
        name => variables.GetValueOrDefault(name);

    private sealed class TemporaryCertificateFile : IDisposable
    {
        public TemporaryCertificateFile(string extension)
        {
            FullName = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Path.GetRandomFileName(), extension));
            File.WriteAllText(FullName, "-----BEGIN CERTIFICATE-----");
        }

        public string FullName { get; }

        public void Dispose() => File.Delete(FullName);
    }
}
