using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Microsoft.Extensions.Configuration;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.Sources;

public sealed class SecretsDirectorySourceTests
{
    [Theory]
    [InlineData("a.pem")]
    [InlineData("b.PFX")]
    [InlineData("c.crt")]
    [InlineData("d.key")]
    public void IsIgnored_CertificateExtension_ReturnsTrue(string fileName)
    {
        Assert.True(SecretsDirectorySource.IsIgnored(fileName));
    }

    [Fact]
    public void IsIgnored_IgnorePrefix_ReturnsTrue()
    {
        Assert.True(SecretsDirectorySource.IsIgnored("ignore.Erp__Platform__Secret"));
    }

    [Fact]
    public void IsIgnored_PlainSecretFileName_ReturnsFalse()
    {
        Assert.False(SecretsDirectorySource.IsIgnored("Erp__Platform__Secret"));
    }

    [Fact]
    public void Add_DirectoryWithCertificateAndSecret_LoadsOnlyTheSecret()
    {
        var directory = Directory.CreateTempSubdirectory("erp-secrets-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "erp-runtime-client.pem"), "-----BEGIN CERTIFICATE-----");
            File.WriteAllText(Path.Combine(directory, "Erp__Platform__Secret"), "secret-value");
            var builder = new ConfigurationBuilder();

            SecretsDirectorySource.Add(builder, directory);
            var configuration = builder.Build();

            Assert.Equal("secret-value", configuration["Erp:Platform:Secret"]);
            Assert.Null(configuration["erp-runtime-client.pem"]);
            Assert.Equal(["Erp"], configuration.GetChildren().Select(c => c.Key));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
