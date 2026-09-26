using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.CreateDownloadLink;

namespace Dewiride.Erp.Modules.Platform.Attachments.UnitTests.Files.Application.Commands.CreateDownloadLink;

public sealed class CreateDownloadLinkHandlerTests
{
    [Fact]
    public async Task HandleAsync_ExistingAttachment_ReturnsTheLinkTheServiceCreatedForIt()
    {
        var link = new DownloadLinkDetails("dGhlIGxpbmsgdG9rZW4", new DateTimeOffset(2026, 9, 26, 7, 0, 0, TimeSpan.Zero));
        var service = new RecordingAttachmentService { Link = link };
        var id = AttachmentId.Create();
        var handler = new CreateDownloadLinkHandler(service);

        var result = await handler.HandleAsync(new CreateDownloadLinkCommand(id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(link, result.Value);
        Assert.Equal(id, service.ReceivedId);
        Assert.Equal(TestContext.Current.CancellationToken, service.ReceivedCancellation);
    }

    [Fact]
    public async Task HandleAsync_UnknownAttachment_ReturnsNotFound()
    {
        var service = new RecordingAttachmentService { Link = AttachmentErrors.NotFound };
        var handler = new CreateDownloadLinkHandler(service);

        var result = await handler.HandleAsync(new CreateDownloadLinkCommand(AttachmentId.Create()), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AttachmentErrors.NotFound, result.Error);
    }
}
