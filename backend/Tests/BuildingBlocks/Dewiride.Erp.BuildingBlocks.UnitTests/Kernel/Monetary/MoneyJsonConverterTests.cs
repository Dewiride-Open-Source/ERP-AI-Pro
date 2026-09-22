using System.Globalization;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Monetary;

public sealed class MoneyJsonConverterTests
{
    [Fact]
    public void Write_Money_IsAnObjectWithAmountAndCurrency()
    {
        var json = JsonSerializer.Serialize(new Money(1234.50m, Currency.Inr));

        Assert.Equal("""{"amount":1234.50,"currency":"INR"}""", json);
    }

    [Fact]
    public void Write_ZeroMinorUnitCurrency_WritesTheWholeAmount()
    {
        var json = JsonSerializer.Serialize(new Money(1234m, Currency.Jpy));

        Assert.Equal("""{"amount":1234,"currency":"JPY"}""", json);
    }

    [Theory]
    [InlineData("1234.50", "INR")]
    [InlineData("-0.01", "USD")]
    [InlineData("1234", "JPY")]
    public void Read_WrittenMoney_RoundTrips(string amount, string code)
    {
        var money = new Money(decimal.Parse(amount, CultureInfo.InvariantCulture), Currency.FromCode(code));

        var restored = JsonSerializer.Deserialize<Money>(JsonSerializer.Serialize(money));

        Assert.Equal(money, restored);
    }

    [Fact]
    public void Read_PropertyNamesInAnyCase_Parses()
    {
        var money = JsonSerializer.Deserialize<Money>("""{"Amount":10,"CURRENCY":"INR"}""");

        Assert.Equal(new Money(10m, Currency.Inr), money);
    }

    [Fact]
    public void Read_PropertiesInEitherOrder_Parses()
    {
        var money = JsonSerializer.Deserialize<Money>("""{"currency":"INR","amount":10}""");

        Assert.Equal(new Money(10m, Currency.Inr), money);
    }

    [Fact]
    public void Read_UnknownProperties_AreSkipped()
    {
        var money = JsonSerializer.Deserialize<Money>("""{"amount":10,"note":{"nested":[1,2,{"deep":true}]},"currency":"INR","tags":["a"]}""");

        Assert.Equal(new Money(10m, Currency.Inr), money);
    }

    [Theory]
    [InlineData("""{"amount":10}""")]
    [InlineData("""{"currency":"INR"}""")]
    [InlineData("""{}""")]
    [InlineData("""{"amount":10,"currency":"XXX"}""")]
    [InlineData("""{"amount":10,"currency":"inr"}""")]
    [InlineData("""{"amount":10,"currency":null}""")]
    [InlineData("""{"amount":"10","currency":"INR"}""")]
    [InlineData("""{"amount":10,"currency":123}""")]
    [InlineData("\"10 INR\"")]
    [InlineData("10")]
    [InlineData("[]")]
    [InlineData("true")]
    public void Read_MalformedMoney_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Money>(json));
    }
}
