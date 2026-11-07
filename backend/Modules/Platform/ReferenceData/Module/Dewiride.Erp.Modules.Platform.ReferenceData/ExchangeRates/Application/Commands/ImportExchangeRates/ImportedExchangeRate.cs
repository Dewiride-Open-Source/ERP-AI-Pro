namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.ImportExchangeRates;

internal sealed record ImportedExchangeRate(string Currency, DateOnly RateDate, decimal Rate, int Unit, string? Source);
