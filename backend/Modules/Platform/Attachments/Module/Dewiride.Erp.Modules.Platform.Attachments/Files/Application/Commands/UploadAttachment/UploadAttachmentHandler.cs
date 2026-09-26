using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.UploadAttachment;

internal sealed class UploadAttachmentHandler(IAttachmentService attachments) : ICommandHandler<UploadAttachmentCommand, AttachmentDetails>
{
    public Task<Result<AttachmentDetails>> HandleAsync(UploadAttachmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return attachments.UploadAsync(new AttachmentUpload(command.FileName, command.ContentType, command.Content), cancellationToken);
    }
}
