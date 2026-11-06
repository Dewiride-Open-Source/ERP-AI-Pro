using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.IntegrationEvents;

public sealed class IntegrationEventsOptionsTests
{
    [Fact]
    public void Validate_Defaults_KeepPassedOnEventsFourHundredDays()
    {
        var options = new IntegrationEventsOptions();

        Assert.True(TryValidate(options));
        Assert.Equal("Erp:Platform:IntegrationEvents", IntegrationEventsOptions.SectionName);
        Assert.Equal(TimeSpan.FromDays(400), options.PublishedEventRetention);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(3650, true)]
    [InlineData(0, false)]
    [InlineData(3651, false)]
    public void Validate_PublishedEventRetention_IsOneDayToTenYears(int days, bool valid)
    {
        Assert.Equal(valid, TryValidate(new IntegrationEventsOptions { PublishedEventRetention = TimeSpan.FromDays(days) }));
    }

    private static bool TryValidate(IntegrationEventsOptions options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true);
}
