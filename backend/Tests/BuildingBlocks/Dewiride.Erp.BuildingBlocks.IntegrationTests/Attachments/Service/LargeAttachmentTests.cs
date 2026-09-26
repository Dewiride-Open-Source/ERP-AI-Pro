using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.Testing;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Service;

public sealed class LargeAttachmentTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private long SizeLimit => factory.Services.GetRequiredService<IOptions<AttachmentsOptions>>().Value.MaxSizeBytes;

    [Fact]
    public async Task UploadAsync_FileSpanningSeveralBlocks_CommitsEveryBlockAndRoundTripsByteForByte()
    {
        var content = TestFiles.Pdf((3 * BlockStagingStream.BlockSize) + 12_345);

        var details = await factory.UploadAsync($"{TestFiles.Stamp()}-ledger.pdf", TestFiles.PdfType, content);

        Assert.Equal(content.Length, details.SizeBytes);
        Assert.Equal(4, await StoredBlobs.CommittedBlockCountAsync(await factory.ContentIdOfAsync(details.Id)));
        AssertSameBytes(content, await factory.DownloadAsync(details.Id));
    }

    [Fact]
    public async Task UploadAsync_FileOfExactlyTheSizeLimit_IsStoredAndRoundTrips()
    {
        var content = TestFiles.Pdf(checked((int)SizeLimit));

        var details = await factory.UploadAsync($"{TestFiles.Stamp()}-archive.pdf", TestFiles.PdfType, content);

        Assert.Equal(SizeLimit, details.SizeBytes);
        AssertSameBytes(content, await factory.DownloadAsync(details.Id));
    }

    [Fact]
    public async Task UploadAsync_OneByteOverTheSizeLimit_FailsTooLargeAndLeavesNoBlobOrReservation()
    {
        var actor = new TestActorContext { ActorId = Guid.CreateVersion7() };
        var store = new RecordingDocumentStore(factory.Services.GetRequiredService<IDocumentStore>());

        var result = await factory.UploadWithAsync($"{TestFiles.Stamp()}-archive.pdf", TestFiles.PdfType, TestFiles.Pdf(checked((int)SizeLimit + 1)), store, actor);

        Assert.Equal("attachment.too-large", result.Error?.Code);
        var abandoned = Assert.Single(store.Uploads);
        Assert.Null(await StoredBlobs.FindAnyAsync(abandoned));
        Assert.False(await factory.HasStoredContentAsync(abandoned));
        Assert.False(await factory.HasReservationsOfAsync(actor.ActorId));
    }

    private static void AssertSameBytes(byte[] expected, byte[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        Assert.True(expected.AsSpan().SequenceEqual(actual), "The downloaded bytes differ from the uploaded file.");
    }
}
