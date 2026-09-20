using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration;

public sealed class AppConfigurationRefreshOptionsTests
{
    [Fact]
    public void Validate_Defaults_AreValid()
    {
        var options = new AppConfigurationRefreshOptions();

        Validate(options);

        Assert.Equal(TimeSpan.FromMinutes(30), options.RefreshInterval);
        Assert.Equal(TimeSpan.FromHours(1), options.SecretRefreshInterval);
        Assert.Equal(TimeSpan.FromMinutes(1), options.StartupTimeout);
    }

    [Theory]
    [InlineData(nameof(AppConfigurationRefreshOptions.RefreshInterval), "00:00:01")]
    [InlineData(nameof(AppConfigurationRefreshOptions.RefreshInterval), "1.00:00:00")]
    [InlineData(nameof(AppConfigurationRefreshOptions.SecretRefreshInterval), "00:01:00")]
    [InlineData(nameof(AppConfigurationRefreshOptions.SecretRefreshInterval), "7.00:00:00")]
    [InlineData(nameof(AppConfigurationRefreshOptions.StartupTimeout), "00:00:01")]
    [InlineData(nameof(AppConfigurationRefreshOptions.StartupTimeout), "00:10:00")]
    public void Validate_ValueOnTheBound_Passes(string property, string value)
    {
        var options = WithValue(property, value);

        Validate(options);
    }

    [Theory]
    [InlineData(nameof(AppConfigurationRefreshOptions.RefreshInterval), "00:00:00.999")]
    [InlineData(nameof(AppConfigurationRefreshOptions.RefreshInterval), "1.00:00:01")]
    [InlineData(nameof(AppConfigurationRefreshOptions.SecretRefreshInterval), "00:00:59")]
    [InlineData(nameof(AppConfigurationRefreshOptions.SecretRefreshInterval), "7.00:00:01")]
    [InlineData(nameof(AppConfigurationRefreshOptions.StartupTimeout), "00:00:00.999")]
    [InlineData(nameof(AppConfigurationRefreshOptions.StartupTimeout), "00:10:01")]
    public void Validate_ValueBeyondTheBound_FailsNamingTheProperty(string property, string value)
    {
        var options = WithValue(property, value);

        var exception = Assert.Throws<ValidationException>(() => Validate(options));

        Assert.Contains(property, exception.Message, StringComparison.Ordinal);
    }

    private static AppConfigurationRefreshOptions WithValue(string property, string value)
    {
        var interval = TimeSpan.Parse(value, CultureInfo.InvariantCulture);

        return property switch
        {
            nameof(AppConfigurationRefreshOptions.RefreshInterval) => new() { RefreshInterval = interval },
            nameof(AppConfigurationRefreshOptions.SecretRefreshInterval) => new() { SecretRefreshInterval = interval },
            nameof(AppConfigurationRefreshOptions.StartupTimeout) => new() { StartupTimeout = interval },
            _ => throw new ArgumentOutOfRangeException(nameof(property), property, "Unknown option."),
        };
    }

    private static void Validate(AppConfigurationRefreshOptions options) =>
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
}
