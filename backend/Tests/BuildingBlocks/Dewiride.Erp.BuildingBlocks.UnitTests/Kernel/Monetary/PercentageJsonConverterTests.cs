using System.Globalization;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Monetary;

public sealed class PercentageJsonConverterTests
{
    [Theory]
    [InlineData("18", "18")]
    [InlineData("12.5", "12.5")]
    [InlineData("0", "0")]
    [InlineData("-2.50", "-2.50")]
    public void Write_Percentage_IsAPlainNumberOfPercent(string percent, string expected)
    {
        var json = JsonSerializer.Serialize(new Percentage(decimal.Parse(percent, CultureInfo.InvariantCulture)));

        Assert.Equal(expected, json);
    }

    [Theory]
    [InlineData("18")]
    [InlineData("12.5")]
    [InlineData("0")]
    [InlineData("-2.5")]
    public void Read_Number_RoundTrips(string json)
    {
        var percentage = JsonSerializer.Deserialize<Percentage>(json);

        Assert.Equal(new Percentage(decimal.Parse(json, CultureInfo.InvariantCulture)), percentage);
        Assert.Equal(percentage, JsonSerializer.Deserialize<Percentage>(JsonSerializer.Serialize(percentage)));
    }

    [Theory]
    [InlineData("\"12.5\"")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("""{"value":12.5}""")]
    public void Read_NotANumber_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Percentage>(json));
    }
}
