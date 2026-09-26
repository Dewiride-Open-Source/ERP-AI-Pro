using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Xunit;

namespace Dewiride.Erp.Testing.Blob;

public sealed class BlobTestContainer : IAsyncLifetime
{
    public const string EmulatorHostVariable = "ERP_TEST_BLOB_EMULATOR_HOST";

    public const string NamePrefix = "erptest-";

    private const int LeftoverAgeHours = 24;

    private static BlobTestContainer? _current;

    private BlobServiceClient? _service;

    public static BlobTestContainer Current =>
        _current ?? throw new InvalidOperationException(
            "No test blob container exists. Declare [assembly: AssemblyFixture(typeof(BlobTestContainer))] in the test project (docs/guides/testing.md) or configure the attachment storage with ErpApiFactory.WithConfiguration.");

    public string EmulatorHost { get; private set; } = string.Empty;

    public string ContainerName { get; private set; } = string.Empty;

    public BlobContainerClient Container =>
        _service?.GetBlobContainerClient(ContainerName) ?? throw new InvalidOperationException("The test blob container is not initialised.");

    public static string ResolveEmulatorHost(Func<string, string?> environmentVariable)
    {
        ArgumentNullException.ThrowIfNull(environmentVariable);

        var host = environmentVariable(EmulatorHostVariable);

        return string.IsNullOrWhiteSpace(host)
            ? throw new InvalidOperationException(
                $"{EmulatorHostVariable} is not set. Run the Azurite blob emulator on port 10000 and set the variable to its host, 127.0.0.1 on a developer machine (docs/guides/testing.md).")
            : host.Trim();
    }

    public async ValueTask InitializeAsync()
    {
        EmulatorHost = ResolveEmulatorHost(Environment.GetEnvironmentVariable);
        _service = new BlobServiceClient($"UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://{EmulatorHost}");
        await DeleteLeftoversAsync(_service);
        ContainerName = $"{NamePrefix}{TimeProvider.System.GetUtcNow():yyyyMMddHHmmss}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";
        await _service.CreateBlobContainerAsync(ContainerName, PublicAccessType.None);
        _current = this;
    }

    public async ValueTask DisposeAsync()
    {
        if (_service is null)
        {
            return;
        }

        _current = null;
        await _service.DeleteBlobContainerAsync(ContainerName);
    }

    private static async Task DeleteLeftoversAsync(BlobServiceClient service)
    {
        var cutoff = TimeProvider.System.GetUtcNow().AddHours(-LeftoverAgeHours);
        await foreach (var container in service.GetBlobContainersAsync(prefix: NamePrefix))
        {
            if (container.Properties.LastModified < cutoff)
            {
                await service.GetBlobContainerClient(container.Name).DeleteIfExistsAsync();
            }
        }
    }
}
