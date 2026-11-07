using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Currencies.Application.Queries.ListCurrencies;

internal sealed record ListCurrenciesQuery : IQuery<IReadOnlyList<Currency>>;
