using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.Hosting;

public sealed class ErpHostOptionsTests
{
    [Fact]
    public void Validate_Defaults_AreValid()
    {
        var options = new ErpHostOptions();

        Validate(options);

        Assert.Equal("Erp:Platform:Host", ErpHostOptions.SectionName);
        Assert.Equal(1024 * 1024, options.MaxRequestBodyBytes);
        Assert.Equal(TimeSpan.FromSeconds(30), options.RequestTimeout);
        Assert.Empty(options.KnownNetworks);
    }

    [Theory]
    [InlineData(nameof(ErpHostOptions.MaxRequestBodyBytes), "1024")]
    [InlineData(nameof(ErpHostOptions.MaxRequestBodyBytes), "1073741824")]
    [InlineData(nameof(ErpHostOptions.RequestTimeout), "00:00:01")]
    [InlineData(nameof(ErpHostOptions.RequestTimeout), "00:10:00")]
    public void Validate_ValueOnTheBound_Passes(string property, string value)
    {
        Validate(WithValue(property, value));
    }

    [Theory]
    [InlineData(nameof(ErpHostOptions.MaxRequestBodyBytes), "1023")]
    [InlineData(nameof(ErpHostOptions.MaxRequestBodyBytes), "1073741825")]
    [InlineData(nameof(ErpHostOptions.MaxRequestBodyBytes), "2147483648")]
    [InlineData(nameof(ErpHostOptions.MaxRequestBodyBytes), "4294967296")]
    [InlineData(nameof(ErpHostOptions.RequestTimeout), "00:00:00.999")]
    [InlineData(nameof(ErpHostOptions.RequestTimeout), "00:10:01")]
    public void Validate_ValueOutsideTheBound_Fails(string property, string value)
    {
        var options = WithValue(property, value);

        var failures = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true));
        Assert.Contains(property, Assert.Single(failures).MemberNames);
    }

    private static void Validate(ErpHostOptions options)
    {
        var failures = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true), string.Join("; ", failures));
    }

    private static ErpHostOptions WithValue(string property, string value)
    {
        var options = new ErpHostOptions();
        var info = typeof(ErpHostOptions).GetProperty(property)!;
        info.SetValue(options, info.PropertyType == typeof(TimeSpan) ? TimeSpan.Parse(value, CultureInfo.InvariantCulture) : long.Parse(value, CultureInfo.InvariantCulture));

        return options;
    }
}
