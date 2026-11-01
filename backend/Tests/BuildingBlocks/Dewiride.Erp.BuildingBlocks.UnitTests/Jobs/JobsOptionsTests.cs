using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Jobs;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs;

public sealed class JobsOptionsTests
{
    [Fact]
    public void Validate_Defaults_KeepFinishedRunsFourHundredDays()
    {
        var options = new JobsOptions();

        Assert.True(TryValidate(options));
        Assert.Equal("Erp:Platform:Jobs", JobsOptions.SectionName);
        Assert.Equal(TimeSpan.FromDays(400), options.FinishedRunRetention);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(3650, true)]
    [InlineData(0, false)]
    [InlineData(3651, false)]
    public void Validate_FinishedRunRetention_IsOneDayToTenYears(int days, bool valid)
    {
        Assert.Equal(valid, TryValidate(new JobsOptions { FinishedRunRetention = TimeSpan.FromDays(days) }));
    }

    private static bool TryValidate(JobsOptions options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true);
}
