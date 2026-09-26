using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.RedeemDownloadLink;

internal sealed class RedeemDownloadLinkHandler(IAttachmentService attachments) : ICommandHandler<RedeemDownloadLinkCommand, AttachmentDownload>
{
    public Task<Result<AttachmentDownload>> HandleAsync(RedeemDownloadLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return attachments.OpenDownloadAsync(command.Id, command.LinkToken, cancellationToken);
    }
}
