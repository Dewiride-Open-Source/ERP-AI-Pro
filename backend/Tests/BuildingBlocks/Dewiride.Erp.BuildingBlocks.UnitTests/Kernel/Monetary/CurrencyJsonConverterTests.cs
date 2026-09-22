using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Monetary;

public sealed class CurrencyJsonConverterTests
{
    [Theory]
    [InlineData("INR")]
    [InlineData("JPY")]
    public void Write_Currency_IsTheCodeString(string code)
    {
        var json = JsonSerializer.Serialize(Currency.FromCode(code));

        Assert.Equal('"' + code + '"', json);
    }

    [Theory]
    [InlineData("INR")]
    [InlineData("USD")]
    public void Read_Code_ReturnsTheTableEntry(string code)
    {
        var currency = JsonSerializer.Deserialize<Currency>('"' + code + '"');

        Assert.Equal(Currency.FromCode(code), currency);
    }

    [Fact]
    public void Serialize_RecordCarryingACurrency_UsesTheAttachedConverter()
    {
        var json = JsonSerializer.Serialize(new Priced(Currency.Inr, new Money(10m, Currency.Inr)));

        Assert.Equal("""{"Currency":"INR","Price":{"amount":10,"currency":"INR"}}""", json);
        Assert.Equal(new Priced(Currency.Inr, new Money(10m, Currency.Inr)), JsonSerializer.Deserialize<Priced>(json));
    }

    [Theory]
    [InlineData("\"XXX\"")]
    [InlineData("\"inr\"")]
    [InlineData("\"\"")]
    [InlineData("null")]
    [InlineData("123")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Read_NotASupportedCode_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Currency>(json));
    }

    private sealed record Priced(Currency Currency, Money Price);
}
