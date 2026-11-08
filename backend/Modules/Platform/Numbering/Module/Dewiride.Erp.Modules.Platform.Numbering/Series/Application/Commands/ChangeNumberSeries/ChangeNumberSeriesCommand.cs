using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Commands.ChangeNumberSeries;

internal sealed record ChangeNumberSeriesCommand(NumberSeriesId Id, NumberFormat Format, long? NextNumber, string Version) : ICommand<NumberedDocumentDetails>;
