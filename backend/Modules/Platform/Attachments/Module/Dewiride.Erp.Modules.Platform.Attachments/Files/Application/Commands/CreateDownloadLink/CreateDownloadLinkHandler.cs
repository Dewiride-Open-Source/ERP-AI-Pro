using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.CreateDownloadLink;

internal sealed class CreateDownloadLinkHandler(IAttachmentService attachments) : ICommandHandler<CreateDownloadLinkCommand, DownloadLinkDetails>
{
    public Task<Result<DownloadLinkDetails>> HandleAsync(CreateDownloadLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return attachments.CreateDownloadLinkAsync(command.Id, cancellationToken);
    }
}
