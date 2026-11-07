using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.ImportExchangeRates;

internal sealed record ImportExchangeRatesCommand(IReadOnlyList<ImportedExchangeRate> Rates) : ICommand<ImportedExchangeRates>;
