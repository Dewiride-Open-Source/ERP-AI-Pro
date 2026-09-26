using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.RateLimiting;

public sealed class RateLimitingOptionsTests
{
    [Fact]
    public void Validate_Defaults_AreValid()
    {
        var options = new RateLimitingOptions();

        Assert.Empty(Validate(options));
        Assert.Equal("Erp:Platform:RateLimiting", RateLimitingOptions.SectionName);
        Assert.True(options.Enabled);
        Assert.Equal(600, options.AnonymousPermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(1), options.AnonymousWindow);
        Assert.Equal(600, options.ActorPermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(1), options.ActorWindow);
        Assert.Equal(6, options.ActorSegmentsPerWindow);
    }

    [Theory]
    [InlineData(nameof(RateLimitingOptions.AnonymousPermitLimit), "0")]
    [InlineData(nameof(RateLimitingOptions.AnonymousWindow), "00:00:00.999")]
    [InlineData(nameof(RateLimitingOptions.AnonymousWindow), "01:00:01")]
    [InlineData(nameof(RateLimitingOptions.ActorPermitLimit), "0")]
    [InlineData(nameof(RateLimitingOptions.ActorWindow), "00:00:00.999")]
    [InlineData(nameof(RateLimitingOptions.ActorWindow), "01:00:01")]
    [InlineData(nameof(RateLimitingOptions.ActorSegmentsPerWindow), "0")]
    [InlineData(nameof(RateLimitingOptions.ActorSegmentsPerWindow), "61")]
    public void Validate_ValueOutsideTheBound_Fails(string property, string value)
    {
        var options = new RateLimitingOptions();
        var info = typeof(RateLimitingOptions).GetProperty(property)!;
        info.SetValue(options, info.PropertyType == typeof(TimeSpan) ? TimeSpan.Parse(value, CultureInfo.InvariantCulture) : int.Parse(value, CultureInfo.InvariantCulture));

        Assert.Contains(property, Assert.Single(Validate(options)).MemberNames);
    }

    private static List<ValidationResult> Validate(RateLimitingOptions options)
    {
        var failures = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true);

        return failures;
    }
}
