using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace Dewiride.Erp.BuildingBlocks.Configuration.Sources;

internal static class SecretsDirectorySource
{
    public const string Directory = "/run/secrets";

    public static readonly string[] CertificateExtensions = [".pem", ".pfx", ".crt", ".key"];

    private const string IgnorePrefix = "ignore.";

    public static bool IsIgnored(string fileName) =>
        fileName.StartsWith(IgnorePrefix, StringComparison.Ordinal)
        || CertificateExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);

    public static void Add(IConfigurationBuilder configuration, string directory) =>
        configuration.AddKeyPerFile(source =>
        {
            source.FileProvider = new FilteredFileProvider(new PhysicalFileProvider(directory), IsIgnored);
            source.Optional = true;
            source.ReloadOnChange = false;
        });
}
