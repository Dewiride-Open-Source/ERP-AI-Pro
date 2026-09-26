using Azure.Storage.Blobs.Specialized;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Blob;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Hosting;

public sealed class AttachmentStorageInitializerTests
{
    [Fact]
    public async Task StartAsync_OnTheEmulator_CreatesTheConfiguredContainer()
    {
        var containerName = $"{BlobTestContainer.NamePrefix}start-{TestFiles.Stamp()}";
        var container = BlobTestContainer.Current.Container.GetParentBlobServiceClient().GetBlobContainerClient(containerName);
        Assert.False((await container.ExistsAsync(TestContext.Current.CancellationToken)).Value);
        await using var factory = new ErpApiFactory().WithConfiguration(ErpApiFactory.AttachmentsContainerNameKey, containerName);

        try
        {
            Assert.Equal(containerName, factory.Services.GetRequiredService<AttachmentBlobClients>().Container.Name);
            Assert.True((await container.ExistsAsync(TestContext.Current.CancellationToken)).Value);
        }
        finally
        {
            await container.DeleteIfExistsAsync(cancellationToken: TestContext.Current.CancellationToken);
        }
    }
}
