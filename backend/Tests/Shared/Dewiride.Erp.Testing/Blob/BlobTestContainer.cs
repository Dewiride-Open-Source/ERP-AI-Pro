using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;
using Xunit;

namespace Dewiride.Erp.Testing.Blob;

public sealed class BlobTestContainer : IAsyncLifetime
{
    public const string EmulatorHostVariable = "ERP_TEST_BLOB_EMULATOR_HOST";

    public const string NamePrefix = "erptest-";

    private static readonly TimeSpan TestProcessLeftoverAge = TimeSpan.FromHours(24);

    private static readonly TimeSpan PresenceLeaseDuration = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan PresenceRenewalInterval = TimeSpan.FromSeconds(20);

    private static BlobTestContainer? _current;

    private readonly string _namePrefix;

    private readonly TimeSpan _leftoverAge;

    private BlobServiceClient? _service;

    private BlobLeaseClient? _presence;

    private PeriodicTimer? _renewal;

    private Task? _renewing;

    public BlobTestContainer()
        : this(NamePrefix, TestProcessLeftoverAge)
    {
    }

    private BlobTestContainer(string namePrefix, TimeSpan leftoverAge)
    {
        _namePrefix = namePrefix;
        _leftoverAge = leftoverAge;
    }

    public static BlobTestContainer Current =>
        _current ?? throw new InvalidOperationException(
            "No test blob container exists. Declare [assembly: AssemblyFixture(typeof(BlobTestContainer))] in the test project (docs/guides/testing.md) or configure the attachment storage with ErpApiFactory.WithConfiguration.");

    public string EmulatorHost { get; private set; } = string.Empty;

    public string ContainerName { get; private set; } = string.Empty;

    public BlobContainerClient Container =>
        _service?.GetBlobContainerClient(ContainerName) ?? throw new InvalidOperationException("The test blob container is not initialised.");

    public static BlobTestContainer WithNamePrefix(string namePrefix, TimeSpan leftoverAge)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namePrefix);
        ArgumentOutOfRangeException.ThrowIfLessThan(leftoverAge, TimeSpan.FromMinutes(1));

        return new BlobTestContainer(namePrefix, leftoverAge);
    }

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
        _service = new BlobServiceClient($"UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://{EmulatorHost}", new BlobClientOptions(AttachmentBlobClients.ServiceVersion));
        await DeleteLeftoversAsync(_service);
        ContainerName = $"{_namePrefix}{TimeProvider.System.GetUtcNow():yyyyMMddHHmmss}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";
        var container = _service.GetBlobContainerClient(ContainerName);
        await container.CreateAsync(PublicAccessType.None);
        _presence = container.GetBlobLeaseClient();
        await _presence.AcquireAsync(PresenceLeaseDuration);
        _renewal = new PeriodicTimer(PresenceRenewalInterval);
        _renewing = RenewPresenceAsync(_presence, _renewal);
        _current = this;
    }

    public async ValueTask DisposeAsync()
    {
        if (_service is null)
        {
            return;
        }

        _current = null;
        _renewal?.Dispose();
        if (_renewing is not null)
        {
            await _renewing;
        }

        await _service.DeleteBlobContainerAsync(ContainerName, new BlobRequestConditions { LeaseId = _presence?.LeaseId });
    }

    private static async Task RenewPresenceAsync(BlobLeaseClient presence, PeriodicTimer renewal)
    {
        while (await renewal.WaitForNextTickAsync())
        {
            await presence.RenewAsync();
        }
    }

    private async Task DeleteLeftoversAsync(BlobServiceClient service)
    {
        var cutoff = TimeProvider.System.GetUtcNow() - _leftoverAge;
        await foreach (var container in service.GetBlobContainersAsync(prefix: _namePrefix))
        {
            if (container.Properties.LastModified < cutoff && container.Properties.LeaseState != LeaseState.Leased)
            {
                await service.GetBlobContainerClient(container.Name).DeleteIfExistsAsync();
            }
        }
    }
}
