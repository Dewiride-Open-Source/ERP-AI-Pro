using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.DeleteAttachment;

namespace Dewiride.Erp.Modules.Platform.Attachments.UnitTests.Files.Application.Commands.DeleteAttachment;

public sealed class DeleteAttachmentHandlerTests
{
    [Fact]
    public async Task HandleAsync_ServiceDeletesTheAttachment_ReturnsTheDeletedId()
    {
        var service = new RecordingAttachmentService();
        var id = AttachmentId.Create();
        var handler = new DeleteAttachmentHandler(service);

        var result = await handler.HandleAsync(new DeleteAttachmentCommand(id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value);
        Assert.Equal(id, service.ReceivedId);
        Assert.Equal(TestContext.Current.CancellationToken, service.ReceivedCancellation);
    }

    [Fact]
    public async Task HandleAsync_ServiceFindsNoAttachment_ReturnsNotFound()
    {
        var service = new RecordingAttachmentService { Deletion = AttachmentErrors.NotFound };
        var handler = new DeleteAttachmentHandler(service);

        var result = await handler.HandleAsync(new DeleteAttachmentCommand(AttachmentId.Create()), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AttachmentErrors.NotFound, result.Error);
    }
}
