using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.UnitTests.ExchangeRates.Domain;

public sealed class ExchangeRateTests
{
    private static readonly DateOnly Today = new(2026, 11, 7);

    [Fact]
    public void Record_ValidRate_KeepsItAsPublishedUnderAVersion7Id()
    {
        var result = ExchangeRate.Record(Currency.Jpy, Today, 56.35m, 100, "  FBIL reference rate ", Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(Currency.Jpy, result.Value.Currency);
        Assert.Equal(Today, result.Value.RateDate);
        Assert.Equal(56.35m, result.Value.Rate);
        Assert.Equal(100, result.Value.Unit);
        Assert.Equal("FBIL reference rate", result.Value.Source);
        Assert.Equal(7, result.Value.Id.Value.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_BlankSource_KeepsNoSource(string? source)
    {
        var result = ExchangeRate.Record(Currency.Usd, Today, 84.1234m, 1, source, Today);

        Assert.Null(result.Value.Source);
    }

    [Fact]
    public void Record_IndianRupee_FailsKeyedByCurrency()
    {
        var result = ExchangeRate.Record(Currency.Inr, Today, 1m, 1, null, Today);

        AssertFailure(result.Error, "exchange-rate.currency-not-foreign", nameof(ExchangeRate.Currency));
    }

    [Fact]
    public void Record_UndefinedCurrency_FailsKeyedByCurrency()
    {
        var result = ExchangeRate.Record(default, Today, 1m, 1, null, Today);

        AssertFailure(result.Error, "exchange-rate.currency-not-foreign", nameof(ExchangeRate.Currency));
    }

    [Fact]
    public void Record_DayAfterToday_FailsKeyedByRateDate()
    {
        var result = ExchangeRate.Record(Currency.Usd, Today.AddDays(1), 84m, 1, null, Today);

        AssertFailure(result.Error, "exchange-rate.date-in-future", nameof(ExchangeRate.RateDate));
    }

    [Fact]
    public void Record_LongAgo_IsAllowed()
    {
        Assert.True(ExchangeRate.Record(Currency.Usd, new DateOnly(2017, 7, 1), 64.5m, 1, null, Today).IsSuccess);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000000.0001")]
    public void Record_RateOutsideTheRange_FailsKeyedByRate(string rate)
    {
        var result = ExchangeRate.Record(Currency.Usd, Today, decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture), 1, null, Today);

        AssertFailure(result.Error, "exchange-rate.rate-out-of-range", nameof(ExchangeRate.Rate));
    }

    [Fact]
    public void Record_RateOfTheLimits_IsAllowed()
    {
        Assert.True(ExchangeRate.Record(Currency.Usd, Today, 0.0001m, 1, null, Today).IsSuccess);
        Assert.True(ExchangeRate.Record(Currency.Usd, Today, ExchangeRate.MaxRate, 1, null, Today).IsSuccess);
    }

    [Fact]
    public void Record_RateWithFiveDecimalPlaces_FailsKeyedByRate()
    {
        var result = ExchangeRate.Record(Currency.Usd, Today, 84.12345m, 1, null, Today);

        AssertFailure(result.Error, "exchange-rate.rate-too-precise", nameof(ExchangeRate.Rate));
    }

    [Fact]
    public void Record_RateWithTrailingZerosBeyondFourPlaces_IsTheSameRate()
    {
        var result = ExchangeRate.Record(Currency.Usd, Today, 84.123400m, 1, null, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(84.1234m, result.Value.Rate);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void Record_PowerOfTenUnit_IsAllowed(int unit)
    {
        Assert.True(ExchangeRate.Record(Currency.FromCode("IDR"), Today, 51.902m, unit, null, Today).IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(50)]
    [InlineData(1_000_000)]
    public void Record_OtherUnit_FailsKeyedByUnit(int unit)
    {
        var result = ExchangeRate.Record(Currency.Usd, Today, 84m, unit, null, Today);

        AssertFailure(result.Error, "exchange-rate.unit-invalid", nameof(ExchangeRate.Unit));
    }

    [Fact]
    public void Record_SourceLongerThanTheLimit_FailsKeyedBySource()
    {
        var result = ExchangeRate.Record(Currency.Usd, Today, 84m, 1, new string('s', ExchangeRate.SourceMaxLength + 1), Today);

        AssertFailure(result.Error, "exchange-rate.source-too-long", nameof(ExchangeRate.Source));
    }

    [Fact]
    public void Correct_ValidValues_ReplacesTheRateUnitAndSource()
    {
        var rate = ExchangeRate.Record(Currency.Usd, Today, 84m, 1, "Bank", Today).Value;

        var result = rate.Correct(8412.5m, 100, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(8412.5m, rate.Rate);
        Assert.Equal(100, rate.Unit);
        Assert.Null(rate.Source);
    }

    [Fact]
    public void Correct_InvalidRate_LeavesTheRateAsItWas()
    {
        var rate = ExchangeRate.Record(Currency.Usd, Today, 84m, 1, "Bank", Today).Value;

        var result = rate.Correct(0m, 1, "Other");

        AssertFailure(result.Error, "exchange-rate.rate-out-of-range", nameof(ExchangeRate.Rate));
        Assert.Equal(84m, rate.Rate);
        Assert.Equal("Bank", rate.Source);
    }

    [Fact]
    public void Quotes_SameRateAndUnit_IsTrueOnlyForBoth()
    {
        var rate = ExchangeRate.Record(Currency.Jpy, Today, 56.35m, 100, null, Today).Value;

        Assert.True(rate.Quotes(56.3500m, 100));
        Assert.False(rate.Quotes(56.35m, 1));
        Assert.False(rate.Quotes(56.36m, 100));
    }

    private static void AssertFailure(BuildingBlocks.Kernel.Results.Error? error, string code, string member)
    {
        Assert.NotNull(error);
        Assert.Equal(code, error.Code);
        Assert.Equal(member, Assert.Single(error.Fields.Keys));
    }
}
