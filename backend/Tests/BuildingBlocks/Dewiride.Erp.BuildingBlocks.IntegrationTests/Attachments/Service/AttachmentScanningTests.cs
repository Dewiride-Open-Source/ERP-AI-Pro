using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Scanning;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Service;

public sealed class AttachmentScanningTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task UploadAsync_ScannerFindsTheFileClean_RecordsCleanAfterScanningExactlyThePlaintext()
    {
        var content = TestFiles.Pdf(300_000);
        var scanner = new FakeAttachmentScanner(AttachmentScanVerdict.Clean);

        var result = await factory.UploadWithAsync($"{TestFiles.Stamp()}-contract.pdf", TestFiles.PdfType, content, scanner);

        Assert.Equal(AttachmentScanStatus.Clean, result.Value.ScanStatus);
        Assert.Equal(AttachmentScanStatus.Clean, await factory.QueryAsync((context, cancellationToken) =>
            context.Attachments.Where(a => a.Id == result.Value.Id).Select(a => a.ScanStatus).SingleAsync(cancellationToken)));
        Assert.Equal(content, scanner.Received);
        Assert.Equal(1, scanner.Starts);
        Assert.Equal(1, scanner.Completions);
        Assert.Equal(1, scanner.Disposals);
    }

    [Fact]
    public async Task UploadAsync_ScannerFindsTheFileInfected_FailsInfectedAndLeavesNoBlobOrReservation()
    {
        var actor = new TestActorContext { ActorId = Guid.CreateVersion7() };
        var store = new RecordingDocumentStore(factory.Services.GetRequiredService<IDocumentStore>());
        var scanner = new FakeAttachmentScanner(AttachmentScanVerdict.Infected);

        var result = await factory.UploadWithAsync(
            $"{TestFiles.Stamp()}-invoice.pdf", TestFiles.PdfType, TestFiles.Pdf(BlockStagingStream.BlockSize + 100_000), store, actor, scanner);

        Assert.Equal("attachment.infected", result.Error?.Code);
        var abandoned = Assert.Single(store.Uploads);
        Assert.Null(await StoredBlobs.FindAnyAsync(abandoned));
        Assert.False(await factory.HasStoredContentAsync(abandoned));
        Assert.False(await factory.HasReservationsOfAsync(actor.ActorId));
        Assert.Equal(1, scanner.Disposals);
    }
}
