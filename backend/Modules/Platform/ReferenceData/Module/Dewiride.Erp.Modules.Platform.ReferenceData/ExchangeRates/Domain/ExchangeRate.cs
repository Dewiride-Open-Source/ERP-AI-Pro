using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;

// A rate is the rupees for Unit units of a foreign currency on one day, kept exactly as its source published it (FBIL quotes
// JPY per 100 and IDR per 10,000 units, to four decimals). A currency has one rate a day among the rates not removed, so a
// rate recorded for the wrong currency or day is removed and recorded again rather than moved.
internal sealed class ExchangeRate : AggregateRoot<ExchangeRateId>, IAuditable, ISoftDeletable, IVersioned
{
    public const int RateScale = 4;

    public const decimal MaxRate = 1_000_000m;

    public const int SourceMaxLength = 200;

    private static readonly int[] QuotationUnits = [1, 10, 100, 1_000, 10_000, 100_000];

    private ExchangeRate(ExchangeRateId id)
        : base(id)
    {
    }

    public static IReadOnlyList<int> Units => QuotationUnits;

    public Currency Currency { get; private set; }

    public DateOnly RateDate { get; private set; }

    public decimal Rate { get; private set; }

    public int Unit { get; private set; }

    public string? Source { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Result<ExchangeRate> Record(Currency currency, DateOnly rateDate, decimal rate, int unit, string? source, DateOnly today)
    {
        if (!currency.IsDefined || currency == Currency.Inr)
        {
            return ExchangeRateErrors.CurrencyNotForeign;
        }

        if (rateDate > today)
        {
            return ExchangeRateErrors.DateInFuture;
        }

        var exchangeRate = new ExchangeRate(ExchangeRateId.Create()) { Currency = currency, RateDate = rateDate };
        var corrected = exchangeRate.Correct(rate, unit, source);

        return corrected.IsFailure ? corrected.Error! : exchangeRate;
    }

    public Result Correct(decimal rate, int unit, string? source)
    {
        if (rate is <= 0 or > MaxRate)
        {
            return ExchangeRateErrors.RateOutOfRange;
        }

        if (decimal.Round(rate, RateScale) != rate)
        {
            return ExchangeRateErrors.RateTooPrecise;
        }

        if (Array.IndexOf(QuotationUnits, unit) < 0)
        {
            return ExchangeRateErrors.UnitInvalid;
        }

        var trimmed = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
        if (trimmed?.Length > SourceMaxLength)
        {
            return ExchangeRateErrors.SourceTooLong;
        }

        Rate = rate;
        Unit = unit;
        Source = trimmed;

        return Result.Success();
    }

    public bool Quotes(decimal rate, int unit) => Rate == rate && Unit == unit;
}
