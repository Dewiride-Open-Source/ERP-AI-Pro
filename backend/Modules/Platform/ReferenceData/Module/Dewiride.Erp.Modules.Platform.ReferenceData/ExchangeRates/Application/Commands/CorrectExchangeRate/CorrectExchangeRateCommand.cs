using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.CorrectExchangeRate;

internal sealed record CorrectExchangeRateCommand(Guid Id, decimal Rate, int Unit, string? Source, string Version) : ICommand<Guid>;
