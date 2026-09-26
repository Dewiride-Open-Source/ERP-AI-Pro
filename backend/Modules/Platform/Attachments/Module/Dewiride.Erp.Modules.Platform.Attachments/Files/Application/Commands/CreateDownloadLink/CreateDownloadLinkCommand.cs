using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.CreateDownloadLink;

internal sealed record CreateDownloadLinkCommand(AttachmentId Id) : ICommand<DownloadLinkDetails>;
