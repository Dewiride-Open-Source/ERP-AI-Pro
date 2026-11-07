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
    [InlineData("CHF", 2)]
    [InlineData("KRW", 0)]
    [InlineData("VND", 0)]
    [InlineData("BHD", 3)]
    [InlineData("KWD", 3)]
    [InlineData("TND", 3)]
    public void FromCode_TableEntry_ExposesTheCodeAndMinorUnits(string code, int minorUnits)
    {
        var currency = Currency.FromCode(code);

        Assert.Equal(code, currency.Code);
        Assert.Equal(minorUnits, currency.MinorUnits);
        Assert.True(currency.IsDefined);
        Assert.Equal(code, currency.ToString());
        Assert.Contains(currency, Currency.All);
    }

    [Theory]
    [InlineData("INR", "Indian Rupee")]
    [InlineData("USD", "US Dollar")]
    [InlineData("GBP", "Pound Sterling")]
    [InlineData("JPY", "Yen")]
    [InlineData("AED", "UAE Dirham")]
    [InlineData("EUR", "Euro")]
    public void Name_TableEntry_IsTheIsoEnglishName(string code, string name)
    {
        Assert.Equal(name, Currency.FromCode(code).Name);
    }

    [Fact]
    public void All_HoldsTheOrdinaryIsoCurrenciesOncePerCodeInCodeOrder()
    {
        var codes = Currency.All.Select(currency => currency.Code).ToList();

        Assert.Equal(155, codes.Count);
        Assert.Equal(codes.Order(StringComparer.Ordinal), codes);
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(Currency.All, currency =>
        {
            Assert.Matches("^[A-Z]{3}$", currency.Code);
            Assert.True(currency.MinorUnits is 0 or 2 or 3, $"{currency.Code} has {currency.MinorUnits} minor units.");
            Assert.False(string.IsNullOrWhiteSpace(currency.Name));
        });
    }

    [Fact]
    public void All_IsExactlyTheOrdinaryCurrenciesOfListOne()
    {
        const string expected =
            "AED AFN ALL AMD AOA ARS AUD AWG AZN BAM BBD BDT BHD BIF BMD BND "
            + "BOB BRL BSD BTN BWP BYN BZD CAD CDF CHF CLP CNY COP CRC CUP CVE "
            + "CZK DJF DKK DOP DZD EGP ERN ETB EUR FJD FKP GBP GEL GHS GIP GMD "
            + "GNF GTQ GYD HKD HNL HTG HUF IDR ILS INR IQD IRR ISK JMD JOD JPY "
            + "KES KGS KHR KMF KPW KRW KWD KYD KZT LAK LBP LKR LRD LSL LYD MAD "
            + "MDL MGA MKD MMK MNT MOP MRU MUR MVR MWK MXN MYR MZN NAD NGN NIO "
            + "NOK NPR NZD OMR PAB PEN PGK PHP PKR PLN PYG QAR RON RSD RUB RWF "
            + "SAR SBD SCR SDG SEK SGD SHP SLE SOS SRD SSP STN SVC SYP SZL THB "
            + "TJS TMT TND TOP TRY TTD TWD TZS UAH UGX USD UYU UZS VED VES VND "
            + "VUV WST XAF XCD XCG XOF XPF YER ZAR ZMW ZWG";

        Assert.Equal(expected, string.Join(' ', Currency.All.Select(currency => currency.Code)));
    }

    [Fact]
    public void All_HoldsEveryNamedCurrency()
    {
        Currency[] named = [Currency.Aed, Currency.Aud, Currency.Cad, Currency.Eur, Currency.Gbp, Currency.Inr, Currency.Jpy, Currency.Sgd, Currency.Usd];

        Assert.All(named, currency => Assert.Contains(currency, Currency.All));
        Assert.All(named, currency => Assert.Equal(Currency.FromCode(currency.Code).Name, currency.Name));
    }

    [Theory]
    [InlineData("XXX")]
    [InlineData("XAU")]
    [InlineData("CLF")]
    [InlineData("XDR")]
    [InlineData("BGN")]
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
