using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Microsoft.Extensions.FileProviders;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.Sources;

public sealed class FilteredFileProviderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("erp-filtered-").FullName;

    public FilteredFileProviderTests()
    {
        File.WriteAllText(Path.Combine(_directory, "erp-runtime-client.pem"), "-----BEGIN CERTIFICATE-----");
        File.WriteAllText(Path.Combine(_directory, "Erp__Platform__Secret"), "secret-value");
        Directory.CreateDirectory(Path.Combine(_directory, "nested.pem"));
    }

    [Fact]
    public void GetDirectoryContents_HiddenFile_IsOmittedWhileDirectoriesStay()
    {
        var provider = new FilteredFileProvider(new PhysicalFileProvider(_directory), SecretsDirectorySource.IsIgnored);

        var contents = provider.GetDirectoryContents(string.Empty);

        Assert.True(contents.Exists);
        Assert.Equal(["Erp__Platform__Secret", "nested.pem"], contents.Select(file => file.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void GetFileInfo_HiddenFile_ReportsNotFound()
    {
        var provider = new FilteredFileProvider(new PhysicalFileProvider(_directory), SecretsDirectorySource.IsIgnored);

        var file = provider.GetFileInfo("erp-runtime-client.pem");

        Assert.False(file.Exists);
        Assert.IsType<NotFoundFileInfo>(file);
    }

    [Fact]
    public void GetFileInfo_PlainFile_ReturnsTheFile()
    {
        var provider = new FilteredFileProvider(new PhysicalFileProvider(_directory), SecretsDirectorySource.IsIgnored);

        var file = provider.GetFileInfo("Erp__Platform__Secret");

        Assert.True(file.Exists);
        Assert.Equal("secret-value", File.ReadAllText(file.PhysicalPath!));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
