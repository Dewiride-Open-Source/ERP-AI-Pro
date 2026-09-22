using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Time;

public sealed class FinancialYearJsonConverterTests
{
    [Theory]
    [InlineData(2026, "\"2026-27\"")]
    [InlineData(1999, "\"1999-00\"")]
    [InlineData(1, "\"0001-02\"")]
    public void Write_Year_IsTheLabelString(int startYear, string expected)
    {
        var json = JsonSerializer.Serialize(new FinancialYear(startYear));

        Assert.Equal(expected, json);
    }

    [Theory]
    [InlineData("\"2026-27\"", 2026)]
    [InlineData("\"1999-00\"", 1999)]
    public void Read_Label_ReturnsTheYear(string json, int expectedStartYear)
    {
        var year = JsonSerializer.Deserialize<FinancialYear>(json);

        Assert.Equal(new FinancialYear(expectedStartYear), year);
        Assert.Equal(year, JsonSerializer.Deserialize<FinancialYear>(JsonSerializer.Serialize(year)));
    }

    [Theory]
    [InlineData("\"2026-28\"")]
    [InlineData("\"26-27\"")]
    [InlineData("\"\"")]
    [InlineData("2026")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("true")]
    public void Read_NotALabel_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FinancialYear>(json));
    }
}
