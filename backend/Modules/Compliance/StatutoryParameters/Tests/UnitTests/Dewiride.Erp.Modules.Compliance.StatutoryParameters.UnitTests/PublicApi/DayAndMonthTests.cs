using System.Text.Json;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.PublicApi;

public sealed class DayAndMonthTests
{
    [Theory]
    [InlineData("04-01", 4, 1)]
    [InlineData("12-31", 12, 31)]
    [InlineData("02-29", 2, 29)]
    public void TryParse_DayAndMonthWrittenMMdd_IsThatDay(string text, int month, int day)
    {
        Assert.True(DayAndMonth.TryParse(text, out var parsed));
        Assert.Equal(new DayAndMonth(month, day), parsed);
        Assert.Equal(text, parsed.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4-1")]
    [InlineData("04-1")]
    [InlineData("13-01")]
    [InlineData("00-10")]
    [InlineData("02-30")]
    [InlineData("04-31")]
    [InlineData("+4-01")]
    [InlineData("04/01")]
    [InlineData("2026-04-01")]
    public void TryParse_TextThatIsNoDayAndMonth_IsRefused(string? text)
    {
        Assert.False(DayAndMonth.TryParse(text, out _));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(13, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 30)]
    [InlineData(4, 31)]
    public void Constructor_DayItsMonthLacks_Throws(int month, int day)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DayAndMonth(month, day));
    }

    [Fact]
    public void Json_DayAndMonth_IsWrittenAndReadAsMMdd()
    {
        var json = JsonSerializer.Serialize(new DayAndMonth(10, 1));

        Assert.Equal("\"10-01\"", json);
        Assert.Equal(new DayAndMonth(10, 1), JsonSerializer.Deserialize<DayAndMonth>(json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DayAndMonth>("\"10-32\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DayAndMonth>("1001"));
    }
}
