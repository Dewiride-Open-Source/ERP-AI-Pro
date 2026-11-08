using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Queries.GetNumberSeries;

internal sealed record GetNumberSeriesQuery(NumberSeriesId Id) : IQuery<NumberedDocumentDetails>;
