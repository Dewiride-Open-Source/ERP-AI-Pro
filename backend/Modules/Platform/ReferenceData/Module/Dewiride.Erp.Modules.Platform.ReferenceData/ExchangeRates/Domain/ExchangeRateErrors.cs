using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;

internal static class ExchangeRateErrors
{
    public static readonly Error CurrencyNotForeign = Field("exchange-rate.currency-not-foreign", nameof(ExchangeRate.Currency), "The currency is a foreign currency, not the Indian rupee.");

    public static readonly Error DateInFuture = Field("exchange-rate.date-in-future", nameof(ExchangeRate.RateDate), "A rate is recorded for today or an earlier day, by India's time.");

    public static readonly Error RateOutOfRange = Field("exchange-rate.rate-out-of-range", nameof(ExchangeRate.Rate), "The rate is more than 0 and at most 1,000,000 rupees.");

    public static readonly Error RateTooPrecise = Field("exchange-rate.rate-too-precise", nameof(ExchangeRate.Rate), $"The rate has at most {ExchangeRate.RateScale} decimal places.");

    public static readonly Error UnitInvalid = Field("exchange-rate.unit-invalid", nameof(ExchangeRate.Unit), "The rate is for 1, 10, 100, 1,000, 10,000 or 100,000 units of the currency.");

    public static readonly Error SourceTooLong = Field("exchange-rate.source-too-long", nameof(ExchangeRate.Source), $"The source of the rate has at most {ExchangeRate.SourceMaxLength} characters.");

    public static readonly Error VersionInvalid = Field("exchange-rate.version-invalid", "Version", "The version is not one the API issued for an exchange rate.");

    public static readonly Error NotFound = Error.NotFound("exchange-rate.not-found", "No exchange rate has this id.");

    public static Error CurrencyUnknown(string code) =>
        Field("exchange-rate.currency-unknown", nameof(ExchangeRate.Currency), $"'{code}' is not the code of an ISO 4217 currency such as USD.");

    public static Error AlreadyRecorded(Currency currency, DateOnly rateDate) =>
        Error.Conflict("exchange-rate.already-recorded", string.Create(CultureInfo.InvariantCulture, $"A rate of {currency} is already recorded for {rateDate:dd-MM-yyyy}; correct that one instead."));

    private static Error Field(string code, string member, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]>(StringComparer.Ordinal) { [member] = [message] });
}
