using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.UploadAttachment;

internal sealed record UploadAttachmentCommand(string? FileName, string? ContentType, Stream Content) : ICommand<AttachmentDetails>;
