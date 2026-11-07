using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Queries.GetExchangeRate;

internal sealed record GetExchangeRateQuery(Guid Id) : IQuery<ExchangeRateDetails>;
