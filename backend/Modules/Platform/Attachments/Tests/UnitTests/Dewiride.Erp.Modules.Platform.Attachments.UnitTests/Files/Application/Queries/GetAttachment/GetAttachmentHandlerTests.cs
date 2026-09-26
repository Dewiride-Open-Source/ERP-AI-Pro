using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.GetAttachment;

namespace Dewiride.Erp.Modules.Platform.Attachments.UnitTests.Files.Application.Queries.GetAttachment;

public sealed class GetAttachmentHandlerTests
{
    [Fact]
    public async Task HandleAsync_ExistingAttachment_ReturnsItsDetails()
    {
        var service = new RecordingAttachmentService();
        var handler = new GetAttachmentHandler(service);

        var result = await handler.HandleAsync(new GetAttachmentQuery(RecordingAttachmentService.SampleDetails.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RecordingAttachmentService.SampleDetails, result.Value);
        Assert.Equal(RecordingAttachmentService.SampleDetails.Id, service.ReceivedId);
        Assert.Equal(TestContext.Current.CancellationToken, service.ReceivedCancellation);
    }

    [Fact]
    public async Task HandleAsync_UnknownAttachment_ReturnsNotFound()
    {
        var service = new RecordingAttachmentService { Details = AttachmentErrors.NotFound };
        var handler = new GetAttachmentHandler(service);

        var result = await handler.HandleAsync(new GetAttachmentQuery(AttachmentId.Create()), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AttachmentErrors.NotFound, result.Error);
    }
}
