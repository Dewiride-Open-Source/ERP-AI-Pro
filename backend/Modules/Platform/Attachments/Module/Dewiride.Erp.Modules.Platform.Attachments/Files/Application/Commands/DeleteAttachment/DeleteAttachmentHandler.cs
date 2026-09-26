using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.DeleteAttachment;

internal sealed class DeleteAttachmentHandler(IAttachmentService attachments) : ICommandHandler<DeleteAttachmentCommand, AttachmentId>
{
    public async Task<Result<AttachmentId>> HandleAsync(DeleteAttachmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var deleted = await attachments.DeleteAsync(command.Id, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? command.Id : deleted.Error!;
    }
}
