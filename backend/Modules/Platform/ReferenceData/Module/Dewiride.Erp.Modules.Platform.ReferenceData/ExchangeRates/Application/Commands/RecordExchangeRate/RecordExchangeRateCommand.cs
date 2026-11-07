using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.RecordExchangeRate;

internal sealed record RecordExchangeRateCommand(string Currency, DateOnly RateDate, decimal Rate, int Unit, string? Source) : ICommand<Guid>;
