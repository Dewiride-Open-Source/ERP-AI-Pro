using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Hosting;

// The attachment settings are validated here, when the API starts, instead of through IStartupValidator: the migrator composes
// the same services but never starts its host, so it needs neither the storage endpoint nor the encryption key.
internal sealed partial class AttachmentStorageInitializer(
    IOptions<AttachmentsOptions> options,
    KeyRing keys,
    AttachmentBlobClients clients,
    ILogger<AttachmentStorageInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var currentKey = keys.Current;

        await clients.PrepareEmulatorAsync(cancellationToken).ConfigureAwait(false);

        LogReady(logger, clients.ContainerUri, currentKey.Id, settings.MaxSizeBytes, clients.UsesEmulator);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Attachment storage ready at {ContainerUri} with encryption key {KeyId} and an upload limit of {MaxSizeBytes} bytes (emulator: {UsesEmulator})")]
    private static partial void LogReady(ILogger logger, Uri containerUri, string keyId, long maxSizeBytes, bool usesEmulator);
}
