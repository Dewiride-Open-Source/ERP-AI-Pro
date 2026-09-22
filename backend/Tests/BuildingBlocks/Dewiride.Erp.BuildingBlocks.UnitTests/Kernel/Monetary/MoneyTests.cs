using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Monetary;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_UndefinedCurrency_ThrowsNamingTheCurrency()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Money(1m, default));

        Assert.Equal("currency", exception.ParamName);
    }

    [Fact]
    public void Constructor_DefinedCurrency_ExposesAmountAndCurrency()
    {
        var money = new Money(1234.5m, Currency.Inr);

        Assert.Equal(1234.5m, money.Amount);
        Assert.Equal(Currency.Inr, money.Currency);
    }

    [Fact]
    public void Zero_Currency_IsZeroInThatCurrency()
    {
        var zero = Money.Zero(Currency.Jpy);

        Assert.Equal(0m, zero.Amount);
        Assert.Equal(Currency.Jpy, zero.Currency);
        Assert.True(zero.IsZero);
    }

    [Theory]
    [InlineData("0", true, false, false)]
    [InlineData("0.00", true, false, false)]
    [InlineData("-0.01", false, true, false)]
    [InlineData("0.01", false, false, true)]
    public void Sign_Amount_ReportsZeroNegativeOrPositive(string amount, bool isZero, bool isNegative, bool isPositive)
    {
        var money = Inr(amount);

        Assert.Equal(isZero, money.IsZero);
        Assert.Equal(isNegative, money.IsNegative);
        Assert.Equal(isPositive, money.IsPositive);
    }

    [Fact]
    public void Add_SameCurrency_SumsTheAmounts()
    {
        var sum = Inr("100.25") + Inr("0.75");

        Assert.Equal(Inr("101"), sum);
        Assert.Equal(sum, Money.Add(Inr("100.25"), Inr("0.75")));
    }

    [Fact]
    public void Subtract_SameCurrency_TakesTheDifference()
    {
        var difference = Inr("100") - Inr("0.75");

        Assert.Equal(Inr("99.25"), difference);
        Assert.Equal(difference, Money.Subtract(Inr("100"), Inr("0.75")));
    }

    [Fact]
    public void Negate_Amount_FlipsTheSignAndKeepsTheCurrency()
    {
        var negated = -Inr("12.34");

        Assert.Equal(Inr("-12.34"), negated);
        Assert.Equal(negated, Money.Negate(Inr("12.34")));
        Assert.Equal(Inr("12.34"), -negated);
    }

    [Fact]
    public void Multiply_Factor_ScalesTheAmountAndKeepsTheCurrency()
    {
        var product = Inr("100") * 1.18m;

        Assert.Equal(Inr("118"), product);
        Assert.Equal(product, 1.18m * Inr("100"));
        Assert.Equal(product, Money.Multiply(Inr("100"), 1.18m));
        Assert.Equal(Currency.Inr, product.Currency);
    }

    [Fact]
    public void Divide_Divisor_ScalesTheAmountAndKeepsTheCurrency()
    {
        var quotient = Inr("118") / 1.18m;

        Assert.Equal(Inr("100"), quotient);
        Assert.Equal(quotient, Money.Divide(Inr("118"), 1.18m));
        Assert.Equal(Currency.Inr, quotient.Currency);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("subtract")]
    [InlineData("compare")]
    [InlineData("less")]
    [InlineData("greater")]
    [InlineData("lessOrEqual")]
    [InlineData("greaterOrEqual")]
    public void Combine_DifferentCurrencies_ThrowsNamingBothCurrencies(string operation)
    {
        var inr = Inr("1");
        var usd = new Money(1m, Currency.Usd);

        var exception = Assert.Throws<InvalidOperationException>(() => Combine(operation, inr, usd));

        Assert.Contains("INR", exception.Message, StringComparison.Ordinal);
        Assert.Contains("USD", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompareTo_SameCurrency_OrdersByAmount()
    {
        Assert.True(Inr("1").CompareTo(Inr("2")) < 0);
        Assert.True(Inr("2").CompareTo(Inr("1")) > 0);
        Assert.Equal(0, Inr("1.50").CompareTo(Inr("1.5")));
    }

    [Fact]
    public void ComparisonOperators_SameCurrency_FollowTheAmount()
    {
        Assert.True(Inr("1") < Inr("2"));
        Assert.True(Inr("2") > Inr("1"));
        Assert.True(Inr("1") <= Inr("1"));
        Assert.True(Inr("1") >= Inr("1"));
        Assert.False(Inr("2") < Inr("1"));
        Assert.False(Inr("1") > Inr("2"));
        Assert.False(Inr("2") <= Inr("1"));
        Assert.False(Inr("1") >= Inr("2"));
    }

    [Theory]
    [InlineData("0.005", "INR", "0.01")]
    [InlineData("0.015", "INR", "0.02")]
    [InlineData("-0.005", "INR", "-0.01")]
    [InlineData("1.234", "INR", "1.23")]
    [InlineData("1.235", "INR", "1.24")]
    [InlineData("1.2", "INR", "1.20")]
    [InlineData("0.5", "JPY", "1")]
    [InlineData("0.4", "JPY", "0")]
    [InlineData("-2.5", "JPY", "-3")]
    public void RoundToMinorUnits_Amount_RoundsAwayFromZeroAtTheCurrencyScale(string amount, string code, string expected)
    {
        var rounded = Of(amount, code).RoundToMinorUnits();

        Assert.Equal(Of(expected, code), rounded);
        Assert.Equal(Currency.FromCode(code), rounded.Currency);
    }

    [Theory]
    [InlineData("0.50", "1")]
    [InlineData("0.49", "0")]
    [InlineData("1.50", "2")]
    [InlineData("-0.50", "-1")]
    [InlineData("-0.49", "0")]
    [InlineData("1234.4999", "1234")]
    public void RoundToWholeUnits_Amount_RoundsFiftyPaiseUpwards(string amount, string expected)
    {
        var rounded = Inr(amount).RoundToWholeUnits();

        Assert.Equal(Inr(expected), rounded);
        Assert.Equal(Currency.Inr, rounded.Currency);
    }

    [Theory]
    [InlineData("1234.5", "INR", "1234.50 INR")]
    [InlineData("1234", "JPY", "1234 JPY")]
    [InlineData("-0.5", "INR", "-0.50 INR")]
    [InlineData("0", "USD", "0.00 USD")]
    [InlineData("1234.567", "INR", "1234.57 INR")]
    public void ToString_Amount_IsInvariantAtTheCurrencyScale(string amount, string code, string expected)
    {
        var money = Of(amount, code);

        Assert.Equal(expected, money.ToString());
        Assert.Equal(expected, money.ToString(null, null));
        Assert.Equal(expected, string.Create(CultureInfo.InvariantCulture, $"{money}"));
    }

    [Fact]
    public void TryFormat_BufferLargeEnough_WritesTheTextAndItsLength()
    {
        Span<char> buffer = stackalloc char[16];

        var formatted = Inr("1234.5").TryFormat(buffer, out var charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(formatted);
        Assert.Equal(11, charsWritten);
        Assert.Equal("1234.50 INR", buffer[..charsWritten].ToString());
    }

    [Fact]
    public void TryFormat_BufferTooSmall_ReturnsFalseWithNothingWritten()
    {
        Span<char> buffer = stackalloc char[10];

        var formatted = Inr("1234.5").TryFormat(buffer, out var charsWritten, default, CultureInfo.InvariantCulture);

        Assert.False(formatted);
        Assert.Equal(0, charsWritten);
    }

    [Theory]
    [InlineData("1234.50 INR", "1234.50", "INR")]
    [InlineData("1234 JPY", "1234", "JPY")]
    [InlineData("-0.5 INR", "-0.5", "INR")]
    [InlineData("  10.00   USD  ", "10", "USD")]
    public void Parse_AmountAndCode_ReturnsTheMoney(string text, string amount, string code)
    {
        var parsed = Money.Parse(text, CultureInfo.InvariantCulture);
        var tried = Money.TryParse(text, out var result);

        Assert.Equal(Of(amount, code), parsed);
        Assert.True(tried);
        Assert.Equal(parsed, result);
    }

    [Theory]
    [InlineData("abc INR")]
    [InlineData("100 XXX")]
    [InlineData("100")]
    [InlineData("100 INR extra")]
    [InlineData("100INR")]
    [InlineData("INR 100")]
    [InlineData("1e3 INR")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_MalformedText_ReturnsFalseWithTheDefault(string text)
    {
        var tried = Money.TryParse(text, CultureInfo.InvariantCulture, out var result);

        Assert.False(tried);
        Assert.Equal(default, result);
        Assert.False(Money.TryParse(text, out _));
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse()
    {
        Assert.False(Money.TryParse(null, CultureInfo.InvariantCulture, out _));
        Assert.False(Money.TryParse(null, out _));
    }

    [Fact]
    public void Parse_MalformedText_ThrowsFormatExceptionNamingTheText()
    {
        var exception = Assert.Throws<FormatException>(() => Money.Parse("100 XXX", CultureInfo.InvariantCulture));

        Assert.Contains("'100 XXX'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Equals_SameValueAtDifferentScales_IsEqualAndSharesTheHashCode()
    {
        var oneAndAHalf = Inr("1.5");
        var oneAndFifty = Inr("1.50");

        Assert.Equal(oneAndAHalf, oneAndFifty);
        Assert.True(oneAndAHalf == oneAndFifty);
        Assert.Equal(oneAndAHalf.GetHashCode(), oneAndFifty.GetHashCode());
    }

    [Fact]
    public void Equals_SameAmountInDifferentCurrencies_IsNotEqual()
    {
        Assert.NotEqual(Inr("1"), new Money(1m, Currency.Usd));
        Assert.True(Inr("1") != new Money(1m, Currency.Usd));
        Assert.NotEqual(Inr("1"), Inr("1.01"));
    }

    private static object Combine(string operation, Money left, Money right) => operation switch
    {
        "add" => left + right,
        "subtract" => left - right,
        "compare" => left.CompareTo(right),
        "less" => left < right,
        "greater" => left > right,
        "lessOrEqual" => left <= right,
        "greaterOrEqual" => left >= right,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation."),
    };

    private static Money Inr(string amount) => Of(amount, "INR");

    private static Money Of(string amount, string code) => new(decimal.Parse(amount, CultureInfo.InvariantCulture), Currency.FromCode(code));
}
