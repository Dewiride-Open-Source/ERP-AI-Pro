using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.RedeemDownloadLink;

namespace Dewiride.Erp.Modules.Platform.Attachments.UnitTests.Files.Application.Commands.RedeemDownloadLink;

public sealed class RedeemDownloadLinkHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidLink_HandsTheIdAndTokenToTheServiceAndReturnsTheDownload()
    {
        await using var download = new AttachmentDownload(new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D]), "statement.pdf", "application/pdf", 5);
        var service = new RecordingAttachmentService { Download = download };
        var id = AttachmentId.Create();
        var handler = new RedeemDownloadLinkHandler(service);

        var result = await handler.HandleAsync(new RedeemDownloadLinkCommand(id, "the-link-token"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(download, result.Value);
        Assert.Equal(id, service.ReceivedId);
        Assert.Equal("the-link-token", service.ReceivedToken);
        Assert.Equal(TestContext.Current.CancellationToken, service.ReceivedCancellation);
    }

    [Fact]
    public async Task HandleAsync_LinkTheServiceRefuses_ReturnsNotFound()
    {
        var handler = new RedeemDownloadLinkHandler(new RecordingAttachmentService { Download = AttachmentErrors.NotFound });

        var result = await handler.HandleAsync(new RedeemDownloadLinkCommand(AttachmentId.Create(), "expired-token"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AttachmentErrors.NotFound, result.Error);
    }
}
