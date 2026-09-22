using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Monetary;

public sealed class MoneyAllocationTests
{
    [Fact]
    public void Allocate_ThreeEqualParts_GivesTheExtraPaisaToTheFirstPart()
    {
        var total = Inr("100.00");

        var parts = total.Allocate(3);

        Assert.Equal([Inr("33.34"), Inr("33.33"), Inr("33.33")], parts);
        AssertSumsToTheRoundedTotal(total, parts);
    }

    [Fact]
    public void Allocate_RatiosOneToTwo_GivesTheExtraPaisaToTheLargestRemainder()
    {
        var total = Inr("100.00");

        var parts = total.Allocate(1, 2);

        Assert.Equal([Inr("33.33"), Inr("66.67")], parts);
        AssertSumsToTheRoundedTotal(total, parts);
    }

    [Fact]
    public void Allocate_TiedRemainders_FavourTheEarlierIndexes()
    {
        var total = Inr("0.02");

        var parts = total.Allocate(4);

        Assert.Equal([Inr("0.01"), Inr("0.01"), Inr("0.00"), Inr("0.00")], parts);
        AssertSumsToTheRoundedTotal(total, parts);
    }

    [Fact]
    public void Allocate_ZeroRatio_ReceivesNothing()
    {
        var total = Inr("100.00");

        var parts = total.Allocate(0, 1, 1);

        Assert.Equal([Inr("0.00"), Inr("50.00"), Inr("50.00")], parts);
        AssertSumsToTheRoundedTotal(total, parts);
    }

    [Fact]
    public void Allocate_ZeroMinorUnitCurrency_StaysWhole()
    {
        var total = new Money(100m, Currency.Jpy);

        var parts = total.Allocate(3);

        Assert.Equal([new Money(34m, Currency.Jpy), new Money(33m, Currency.Jpy), new Money(33m, Currency.Jpy)], parts);
        Assert.All(parts, part => Assert.Equal(decimal.Truncate(part.Amount), part.Amount));
        AssertSumsToTheRoundedTotal(total, parts);
    }

    [Fact]
    public void Allocate_NegativeAmount_AllocatesToTheSameNegativeTotal()
    {
        var total = Inr("-100.00");

        var parts = total.Allocate(3);

        Assert.Equal([Inr("-33.34"), Inr("-33.33"), Inr("-33.33")], parts);
        AssertSumsToTheRoundedTotal(total, parts);
    }

    [Fact]
    public void Allocate_AmountBeyondTheMinorUnits_RoundsTheTotalFirst()
    {
        var total = Inr("10.005");

        var parts = total.Allocate(2);

        Assert.Equal([Inr("5.01"), Inr("5.00")], parts);
        Assert.Equal(Inr("10.01"), parts.Aggregate(Money.Zero(Currency.Inr), (sum, part) => sum + part));
        AssertSumsToTheRoundedTotal(total, parts);
    }

    [Theory]
    [InlineData("100.00", "INR", 3)]
    [InlineData("0.01", "INR", 3)]
    [InlineData("99.99", "INR", 7)]
    [InlineData("1000", "JPY", 7)]
    [InlineData("-7.77", "INR", 4)]
    [InlineData("0", "INR", 5)]
    [InlineData("123456.78", "USD", 1)]
    public void Allocate_Parts_SumsExactlyToTheRoundedTotalAtTheCurrencyScale(string amount, string code, int count)
    {
        var total = new Money(decimal.Parse(amount, CultureInfo.InvariantCulture), Currency.FromCode(code));

        var parts = total.Allocate(count);

        Assert.Equal(count, parts.Count);
        Assert.All(parts, part => Assert.Equal(part.RoundToMinorUnits(), part));
        Assert.All(parts, part => Assert.Equal(total.Currency, part.Currency));
        AssertSumsToTheRoundedTotal(total, parts);
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { -1, 2 })]
    [InlineData(new[] { 1, -1 })]
    [InlineData(new[] { 0, 0 })]
    [InlineData(new[] { 0 })]
    public void Allocate_InvalidRatios_ThrowsNamingTheRatios(int[] ratios)
    {
        var exception = Assert.Throws<ArgumentException>(() => Inr("100.00").Allocate(ratios));

        Assert.Equal("ratios", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Allocate_NonPositivePartCount_ThrowsNamingTheParts(int parts)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Inr("100.00").Allocate(parts));

        Assert.Equal("parts", exception.ParamName);
    }

    [Fact]
    public void Allocate_NullRatios_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Inr("100.00").Allocate(null!));
    }

    private static void AssertSumsToTheRoundedTotal(Money total, IReadOnlyList<Money> parts)
    {
        var sum = parts.Aggregate(Money.Zero(total.Currency), (running, part) => running + part);

        Assert.Equal(total.RoundToMinorUnits(), sum);
    }

    private static Money Inr(string amount) => new(decimal.Parse(amount, CultureInfo.InvariantCulture), Currency.Inr);
}
