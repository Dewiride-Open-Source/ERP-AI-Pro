using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Commands.SetUpNumberSeries;

internal sealed record SetUpNumberSeriesCommand(string DocumentType, NumberFormat Format, long NextNumber) : ICommand<NumberedDocumentDetails>;
