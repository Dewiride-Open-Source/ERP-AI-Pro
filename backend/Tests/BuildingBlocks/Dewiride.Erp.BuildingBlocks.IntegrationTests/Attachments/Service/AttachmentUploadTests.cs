using System.Security.Cryptography;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Service;

public sealed class AttachmentUploadTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    public static TheoryData<string?, string?, string, string> RejectedFiles { get; } = new()
    {
        { null, TestFiles.TextType, "hello", "attachment.file-name-invalid" },
        { "   ", TestFiles.TextType, "hello", "attachment.file-name-invalid" },
        { "..", TestFiles.TextType, "hello", "attachment.file-name-invalid" },
        { "scans/", TestFiles.TextType, "hello", "attachment.file-name-invalid" },
        { new string([(char)0x202E, (char)0x200B]), TestFiles.TextType, "hello", "attachment.file-name-invalid" },
        { $"{new string('a', Attachment.FileNameMaxLength - 3)}.txt", TestFiles.TextType, "hello", "attachment.file-name-invalid" },
        { "page.html", "text/html", "<p>hello</p>", "attachment.unsupported-type" },
        { "notes.txt", null, "hello", "attachment.unsupported-type" },
        { "scan.pdf", TestFiles.PdfType, "hello", "attachment.content-mismatch" },
        { "notes.txt", TestFiles.TextType, "nul\0byte", "attachment.content-mismatch" },
        { "empty.txt", TestFiles.TextType, string.Empty, "attachment.empty" },
    };

    [Fact]
    public async Task UploadAsync_NewFile_StoresOneEncryptedBlobAndRecordsItsContent()
    {
        var stamp = TestFiles.Stamp();
        var marker = $"payslip-{stamp}";
        var plaintext = TestFiles.Text(marker, 20_000);
        var sha256 = SHA256.HashData(plaintext);

        var details = await factory.UploadAsync($"{stamp} payslip.txt", "Text/Plain; charset=utf-8", plaintext);

        var attachment = await factory.QueryAsync((context, cancellationToken) =>
            context.Attachments.AsNoTracking().Include(a => a.Content).SingleAsync(a => a.Id == details.Id, cancellationToken));
        Assert.Equal($"{stamp} payslip.txt", attachment.FileName);
        Assert.Equal(TestFiles.TextType, attachment.ContentType);
        Assert.Equal(AttachmentScanStatus.NotScanned, attachment.ScanStatus);
        Assert.Equal(sha256, attachment.Content.Sha256);
        Assert.Equal(plaintext.Length, attachment.Content.Length);
        Assert.Equal(factory.Services.GetRequiredService<KeyRing>().Current.Id, attachment.Content.KeyId);
        Assert.False(await factory.QueryAsync((context, cancellationToken) => context.UploadReservations.AnyAsync(r => r.ContentId == attachment.ContentId, cancellationToken)));
        Assert.Equal(
            new AttachmentDetails(attachment.Id, attachment.FileName, TestFiles.TextType, plaintext.Length, Convert.ToHexStringLower(sha256), AttachmentScanStatus.NotScanned, attachment.CreatedAt, ActorIds.System),
            details);

        var blob = await StoredBlobs.ReadAsync(attachment.ContentId);
        Assert.True(blob.AsSpan().StartsWith("ERPA"u8));
        Assert.Equal(-1, blob.AsSpan().IndexOf(Encoding.ASCII.GetBytes(marker)));
    }

    [Fact]
    public async Task UploadAsync_ContentAlreadyStored_ReusesItAndDiscardsTheStagedCopy()
    {
        var stamp = TestFiles.Stamp();
        var content = TestFiles.Pdf(BlockStagingStream.BlockSize + 100_000);
        var sha256 = SHA256.HashData(content);
        var original = await factory.UploadAsync($"{stamp}-original.pdf", TestFiles.PdfType, content);
        var store = new RecordingDocumentStore(factory.Services.GetRequiredService<IDocumentStore>());

        var copy = await factory.UploadWithAsync($"{stamp}-copy.pdf", TestFiles.PdfType, content, store);

        Assert.True(copy.IsSuccess);
        Assert.Equal(original.Sha256, copy.Value.Sha256);
        var storedContent = await factory.ContentIdOfAsync(original.Id);
        Assert.Equal(storedContent, await factory.ContentIdOfAsync(copy.Value.Id));
        Assert.Equal(1, await factory.QueryAsync((context, cancellationToken) => context.StoredContents.CountAsync(c => c.Sha256 == sha256, cancellationToken)));
        Assert.True(await StoredBlobs.IsCommittedAsync(storedContent));
        var discarded = Assert.Single(store.Uploads);
        Assert.NotEqual(storedContent, discarded);
        Assert.Null(await StoredBlobs.FindAnyAsync(discarded));
        Assert.False(await factory.QueryAsync((context, cancellationToken) => context.UploadReservations.AnyAsync(r => r.ContentId == discarded, cancellationToken)));
    }

    [Fact]
    public async Task UploadAsync_DifferentContent_StoresASecondBlob()
    {
        var stamp = TestFiles.Stamp();

        var first = await factory.UploadAsync($"{stamp}-first.txt", TestFiles.TextType, TestFiles.Text($"first-{stamp}", 4_000));
        var second = await factory.UploadAsync($"{stamp}-second.txt", TestFiles.TextType, TestFiles.Text($"second-{stamp}", 4_000));

        var firstContent = await factory.ContentIdOfAsync(first.Id);
        var secondContent = await factory.ContentIdOfAsync(second.Id);
        Assert.NotEqual(firstContent, secondContent);
        Assert.NotEqual(first.Sha256, second.Sha256);
        Assert.True(await StoredBlobs.IsCommittedAsync(firstContent));
        Assert.True(await StoredBlobs.IsCommittedAsync(secondContent));
    }

    [Fact]
    public async Task UploadAsync_RequestAbortedMidTransfer_RemovesTheStagedBlocksAndTheReservation()
    {
        var actor = new TestActorContext { ActorId = Guid.CreateVersion7() };
        var store = new RecordingDocumentStore(factory.Services.GetRequiredService<IDocumentStore>());
        using var request = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var body = new DisconnectingBody(TestFiles.Pdf(2 * BlockStagingStream.BlockSize), BlockStagingStream.BlockSize + 100_000, request);
        await using var scope = factory.Services.CreateAsyncScope();
        var uploader = ActivatorUtilities.CreateInstance<AttachmentUploader>(scope.ServiceProvider, store, actor);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            uploader.UploadAsync(new AttachmentUpload($"{TestFiles.Stamp()}-payroll.pdf", TestFiles.PdfType, body), request.Token));

        var abandoned = Assert.Single(store.Uploads);
        Assert.Null(await StoredBlobs.FindAnyAsync(abandoned));
        Assert.False(await factory.HasStoredContentAsync(abandoned));
        Assert.False(await factory.HasReservationsOfAsync(actor.ActorId));
    }

    [Theory]
    [MemberData(nameof(RejectedFiles))]
    public async Task UploadAsync_RejectedFile_FailsBeforeReservingOrStoringAnything(string? fileName, string? contentType, string content, string code)
    {
        var actor = new TestActorContext { ActorId = Guid.CreateVersion7() };
        var store = new RecordingDocumentStore(factory.Services.GetRequiredService<IDocumentStore>());

        var result = await factory.UploadWithAsync(fileName, contentType, Encoding.UTF8.GetBytes(content), store, actor);

        Assert.Equal(code, result.Error?.Code);
        Assert.Empty(store.Uploads);
        Assert.False(await factory.HasReservationsOfAsync(actor.ActorId));
    }
}
