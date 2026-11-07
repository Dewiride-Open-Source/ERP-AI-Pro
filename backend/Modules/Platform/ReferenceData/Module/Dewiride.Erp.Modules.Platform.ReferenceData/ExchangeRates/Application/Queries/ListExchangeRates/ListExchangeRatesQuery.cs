using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Queries.ListExchangeRates;

internal sealed record ListExchangeRatesQuery(ListRequest Request) : IQuery<PagedResult<ExchangeRateDetails>>;
