using Azure.Core;
using Azure.Storage.Blobs;
using Dewiride.Erp.BuildingBlocks.Observability.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;

internal sealed class AttachmentBlobClients
{
    // Pinned so a client library upgrade cannot send a service version newer than the Azurite image the tests run against
    // understands; the pin and the image digest move together.
    public const BlobClientOptions.ServiceVersion ServiceVersion = BlobClientOptions.ServiceVersion.V2026_06_06;

    private AttachmentBlobClients(BlobContainerClient container, BlobContainerClient probe, bool usesEmulator)
    {
        Container = container;
        Probe = probe;
        UsesEmulator = usesEmulator;
    }

    public BlobContainerClient Container { get; }

    public BlobContainerClient Probe { get; }

    public bool UsesEmulator { get; }

    public static AttachmentBlobClients Create(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var settings = services.GetRequiredService<IOptions<AttachmentsOptions>>().Value;
        var usesEmulator = !string.IsNullOrWhiteSpace(settings.EmulatorHost);
        var probeOptions = new BlobClientOptions(ServiceVersion);
        probeOptions.Retry.MaxRetries = 0;
        probeOptions.Retry.NetworkTimeout = HealthEndpoints.CheckTimeout;

        return new AttachmentBlobClients(
            Connect(settings, services, new BlobClientOptions(ServiceVersion)),
            Connect(settings, services, probeOptions),
            usesEmulator);
    }

    // The emulator is reached through its well-known development account, whose key the client library carries itself, so no
    // storage key exists in configuration; a real account accepts Microsoft Entra tokens only.
    private static BlobContainerClient Connect(AttachmentsOptions settings, IServiceProvider services, BlobClientOptions options) =>
        string.IsNullOrWhiteSpace(settings.EmulatorHost)
            ? new BlobServiceClient(new Uri(settings.BlobServiceUri!), services.GetRequiredService<TokenCredential>(), options).GetBlobContainerClient(settings.ContainerName)
            : new BlobContainerClient($"UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://{settings.EmulatorHost.Trim()}", settings.ContainerName, options);
}
