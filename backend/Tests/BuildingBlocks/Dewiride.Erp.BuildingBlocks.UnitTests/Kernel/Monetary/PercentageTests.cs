using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Monetary;

public sealed class PercentageTests
{
    [Fact]
    public void Constants_ZeroAndHundred_HoldTheirPercentValues()
    {
        Assert.Equal(0m, Percentage.Zero.Value);
        Assert.Equal(100m, Percentage.Hundred.Value);
        Assert.Equal(1m, Percentage.Hundred.AsFraction);
    }

    [Theory]
    [InlineData("18", "0.18")]
    [InlineData("12.5", "0.125")]
    [InlineData("0", "0")]
    [InlineData("100", "1")]
    [InlineData("0.01", "0.0001")]
    public void AsFraction_Percent_DividesByAHundredAndRoundTripsThroughFromFraction(string percent, string fraction)
    {
        var percentage = Percent(percent);

        Assert.Equal(Number(fraction), percentage.AsFraction);
        Assert.Equal(percentage, Percentage.FromFraction(percentage.AsFraction));
        Assert.Equal(percentage, Percentage.FromFraction(Number(fraction)));
    }

    [Theory]
    [InlineData("18", "1000", "180")]
    [InlineData("12.5", "200", "25")]
    [InlineData("0", "1000", "0")]
    [InlineData("100", "1000", "1000")]
    [InlineData("18", "-100", "-18")]
    [InlineData("2.5", "33.33", "0.83325")]
    public void Of_Money_ScalesTheAmountWithoutRoundingAndKeepsTheCurrency(string percent, string amount, string expected)
    {
        var money = new Money(Number(amount), Currency.Inr);

        var share = Percent(percent).Of(money);

        Assert.Equal(new Money(Number(expected), Currency.Inr), share);
        Assert.Equal(Currency.Inr, share.Currency);
    }

    [Fact]
    public void Add_TwoPercentages_SumsTheValues()
    {
        var sum = Percent("9") + Percent("9");

        Assert.Equal(Percent("18"), sum);
        Assert.Equal(sum, Percentage.Add(Percent("9"), Percent("9")));
    }

    [Fact]
    public void Subtract_TwoPercentages_TakesTheDifference()
    {
        var difference = Percent("18") - Percent("12.5");

        Assert.Equal(Percent("5.5"), difference);
        Assert.Equal(difference, Percentage.Subtract(Percent("18"), Percent("12.5")));
    }

    [Fact]
    public void ComparisonOperators_Percentages_FollowTheValue()
    {
        Assert.True(Percent("5") < Percent("12"));
        Assert.True(Percent("12") > Percent("5"));
        Assert.True(Percent("5") <= Percent("5"));
        Assert.True(Percent("5") >= Percent("5.0"));
        Assert.False(Percent("12") < Percent("5"));
        Assert.False(Percent("5") > Percent("12"));
        Assert.False(Percent("12") <= Percent("5"));
        Assert.False(Percent("5") >= Percent("12"));
    }

    [Fact]
    public void CompareTo_Percentages_OrdersByValue()
    {
        Assert.True(Percent("5").CompareTo(Percent("12")) < 0);
        Assert.True(Percent("12").CompareTo(Percent("5")) > 0);
        Assert.Equal(0, Percent("5").CompareTo(Percent("5.00")));
    }

    [Fact]
    public void Equals_SameValueAtDifferentScales_IsEqual()
    {
        Assert.Equal(Percent("12.5"), Percent("12.50"));
        Assert.NotEqual(Percent("12.5"), Percent("12.51"));
    }

    [Theory]
    [InlineData("18", "18%")]
    [InlineData("12.5", "12.5%")]
    [InlineData("0", "0%")]
    [InlineData("100", "100%")]
    [InlineData("0.125", "0.125%")]
    [InlineData("18.00", "18%")]
    [InlineData("-5", "-5%")]
    public void ToString_Value_IsThePercentWithoutTrailingZeros(string percent, string expected)
    {
        Assert.Equal(expected, Percent(percent).ToString());
    }

    private static Percentage Percent(string value) => new(Number(value));

    private static decimal Number(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
