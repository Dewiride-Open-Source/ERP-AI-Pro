using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.DeleteAttachment;

internal sealed record DeleteAttachmentCommand(AttachmentId Id) : ICommand<AttachmentId>;
