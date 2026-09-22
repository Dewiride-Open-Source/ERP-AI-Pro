using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Monetary;

public sealed class CurrencyTests
{
    [Theory]
    [InlineData("INR", 2)]
    [InlineData("USD", 2)]
    [InlineData("EUR", 2)]
    [InlineData("GBP", 2)]
    [InlineData("AED", 2)]
    [InlineData("SGD", 2)]
    [InlineData("AUD", 2)]
    [InlineData("CAD", 2)]
    [InlineData("JPY", 0)]
    public void FromCode_TableEntry_ExposesTheCodeAndMinorUnits(string code, int minorUnits)
    {
        var currency = Currency.FromCode(code);

        Assert.Equal(code, currency.Code);
        Assert.Equal(minorUnits, currency.MinorUnits);
        Assert.True(currency.IsDefined);
        Assert.Equal(code, currency.ToString());
        Assert.Contains(currency, Currency.All);
    }

    [Fact]
    public void All_HoldsExactlyTheStaticTableEntries()
    {
        Currency[] expected = [Currency.Aed, Currency.Aud, Currency.Cad, Currency.Eur, Currency.Gbp, Currency.Inr, Currency.Jpy, Currency.Sgd, Currency.Usd];

        Assert.Equal(expected, Currency.All.OrderBy(currency => currency.Code, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("XXX")]
    [InlineData("inr")]
    [InlineData("")]
    [InlineData("INRR")]
    public void FromCode_UnknownCode_ThrowsNamingTheCode(string code)
    {
        var exception = Assert.Throws<ArgumentException>(() => Currency.FromCode(code));

        Assert.Equal("code", exception.ParamName);
        Assert.Contains($"'{code}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("INR", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("XXX")]
    [InlineData("inr")]
    [InlineData("")]
    public void TryFromCode_NullOrUnknownCode_ReturnsFalseWithTheDefault(string? code)
    {
        var found = Currency.TryFromCode(code, out var currency);

        Assert.False(found);
        Assert.Equal(default, currency);
        Assert.False(currency.IsDefined);
    }

    [Fact]
    public void TryFromCode_KnownCode_ReturnsTrueWithTheCurrency()
    {
        var found = Currency.TryFromCode("JPY", out var currency);

        Assert.True(found);
        Assert.Equal(Currency.Jpy, currency);
    }

    [Fact]
    public void Equals_SameCode_IsEqualAndSharesTheHashCode()
    {
        var fromCode = Currency.FromCode("INR");

        Assert.Equal(Currency.Inr, fromCode);
        Assert.True(Currency.Inr == fromCode);
        Assert.Equal(Currency.Inr.GetHashCode(), fromCode.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentCodes_IsNotEqual()
    {
        Assert.NotEqual(Currency.Inr, Currency.Usd);
        Assert.True(Currency.Inr != Currency.Usd);
        Assert.NotEqual(Currency.Inr, default);
    }

    [Fact]
    public void IsDefined_DefaultValue_IsFalseWithAnEmptyCode()
    {
        var currency = default(Currency);

        Assert.False(currency.IsDefined);
        Assert.Equal(string.Empty, currency.ToString());
        Assert.Equal(default(Currency).GetHashCode(), currency.GetHashCode());
    }

    [Fact]
    public void CodeLength_IsThreeLikeEveryIsoCode()
    {
        Assert.Equal(3, Currency.CodeLength);
        Assert.All(Currency.All, currency => Assert.Equal(Currency.CodeLength, currency.Code.Length));
    }
}
