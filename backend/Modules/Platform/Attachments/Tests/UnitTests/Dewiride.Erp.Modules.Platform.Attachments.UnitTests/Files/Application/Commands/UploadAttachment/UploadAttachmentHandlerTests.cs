using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.UploadAttachment;

namespace Dewiride.Erp.Modules.Platform.Attachments.UnitTests.Files.Application.Commands.UploadAttachment;

public sealed class UploadAttachmentHandlerTests
{
    [Fact]
    public async Task HandleAsync_File_HandsTheNameTypeAndContentToTheServiceAndReturnsTheStoredDetails()
    {
        var service = new RecordingAttachmentService();
        using var content = new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D]);
        var handler = new UploadAttachmentHandler(service);

        var result = await handler.HandleAsync(new UploadAttachmentCommand("statement.pdf", "application/pdf", content), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RecordingAttachmentService.SampleDetails, result.Value);
        Assert.Equal(new AttachmentUpload("statement.pdf", "application/pdf", content), service.ReceivedUpload);
        Assert.Equal(TestContext.Current.CancellationToken, service.ReceivedCancellation);
    }

    [Fact]
    public async Task HandleAsync_ServiceRejectsTheFile_ReturnsTheRejection()
    {
        var service = new RecordingAttachmentService { Details = AttachmentErrors.ContentMismatch };
        using var content = new MemoryStream([0x00]);
        var handler = new UploadAttachmentHandler(service);

        var result = await handler.HandleAsync(new UploadAttachmentCommand("image.png", "image/png", content), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AttachmentErrors.ContentMismatch, result.Error);
    }
}
