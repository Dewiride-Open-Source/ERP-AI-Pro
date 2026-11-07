using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.RemoveExchangeRate;

internal sealed record RemoveExchangeRateCommand(Guid Id) : ICommand<Guid>;
