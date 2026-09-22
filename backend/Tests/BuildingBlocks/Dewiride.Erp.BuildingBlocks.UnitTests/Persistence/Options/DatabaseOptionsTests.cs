using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Options;

public sealed class DatabaseOptionsTests
{
    [Fact]
    public void Validate_Defaults_AreValid()
    {
        var options = new DatabaseOptions();

        Validate(options);

        Assert.Null(options.ConnectionString);
        Assert.Equal(DatabaseProvider.SqlServer, options.Provider);
        Assert.Equal(TimeSpan.FromSeconds(30), options.CommandTimeout);
        Assert.Equal(5, options.MaxRetryCount);
        Assert.Equal(TimeSpan.FromSeconds(10), options.MaxRetryDelay);
    }

    [Theory]
    [InlineData(nameof(DatabaseOptions.CommandTimeout), "00:00:01")]
    [InlineData(nameof(DatabaseOptions.CommandTimeout), "00:10:00")]
    [InlineData(nameof(DatabaseOptions.MaxRetryCount), "0")]
    [InlineData(nameof(DatabaseOptions.MaxRetryCount), "20")]
    [InlineData(nameof(DatabaseOptions.MaxRetryDelay), "00:00:01")]
    [InlineData(nameof(DatabaseOptions.MaxRetryDelay), "00:02:00")]
    public void Validate_ValueOnTheBound_Passes(string property, string value)
    {
        var options = WithValue(property, value);

        Validate(options);
    }

    [Theory]
    [InlineData(nameof(DatabaseOptions.CommandTimeout), "00:00:00.999")]
    [InlineData(nameof(DatabaseOptions.CommandTimeout), "00:10:01")]
    [InlineData(nameof(DatabaseOptions.MaxRetryCount), "-1")]
    [InlineData(nameof(DatabaseOptions.MaxRetryCount), "21")]
    [InlineData(nameof(DatabaseOptions.MaxRetryDelay), "00:00:00.999")]
    [InlineData(nameof(DatabaseOptions.MaxRetryDelay), "00:02:01")]
    public void Validate_ValueBeyondTheBound_FailsNamingTheProperty(string property, string value)
    {
        var options = WithValue(property, value);

        var exception = Assert.Throws<ValidationException>(() => Validate(options));

        Assert.Contains(property, exception.Message, StringComparison.Ordinal);
    }

    private static DatabaseOptions WithValue(string property, string value) =>
        property switch
        {
            nameof(DatabaseOptions.CommandTimeout) => new() { CommandTimeout = TimeSpan.Parse(value, CultureInfo.InvariantCulture) },
            nameof(DatabaseOptions.MaxRetryCount) => new() { MaxRetryCount = int.Parse(value, CultureInfo.InvariantCulture) },
            nameof(DatabaseOptions.MaxRetryDelay) => new() { MaxRetryDelay = TimeSpan.Parse(value, CultureInfo.InvariantCulture) },
            _ => throw new ArgumentOutOfRangeException(nameof(property), property, "Unknown option."),
        };

    private static void Validate(DatabaseOptions options) =>
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
}
