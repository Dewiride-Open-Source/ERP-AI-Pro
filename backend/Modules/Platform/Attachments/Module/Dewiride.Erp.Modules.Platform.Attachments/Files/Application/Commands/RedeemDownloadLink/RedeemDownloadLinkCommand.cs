using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.RedeemDownloadLink;

internal sealed record RedeemDownloadLinkCommand(AttachmentId Id, string? LinkToken) : ICommand<AttachmentDownload>;
