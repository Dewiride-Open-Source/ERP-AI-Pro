using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Auditing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Auditing;

public sealed class AuditingOptionsTests
{
    [Fact]
    public void Validate_Defaults_AreValidAndKeepSecurityEventsForAYear()
    {
        var options = new AuditingOptions();

        Validate(options);

        Assert.Equal("Erp:Platform:Auditing", AuditingOptions.SectionName);
        Assert.Equal(TimeSpan.FromDays(365), options.SecurityEventRetention);
        Assert.Equal(TimeSpan.FromDays(1), options.SweepInterval);
    }

    [Theory]
    [InlineData(nameof(AuditingOptions.SecurityEventRetention), "1.00:00:00")]
    [InlineData(nameof(AuditingOptions.SecurityEventRetention), "3650.00:00:00")]
    [InlineData(nameof(AuditingOptions.SweepInterval), "00:01:00")]
    [InlineData(nameof(AuditingOptions.SweepInterval), "7.00:00:00")]
    public void Validate_ValueOnTheBound_Passes(string property, string value)
    {
        Validate(WithValue(property, value));
    }

    [Theory]
    [InlineData(nameof(AuditingOptions.SecurityEventRetention), "23:59:59")]
    [InlineData(nameof(AuditingOptions.SecurityEventRetention), "3650.00:00:01")]
    [InlineData(nameof(AuditingOptions.SweepInterval), "00:00:59")]
    [InlineData(nameof(AuditingOptions.SweepInterval), "7.00:00:01")]
    public void Validate_ValueOutsideTheBound_Fails(string property, string value)
    {
        var options = WithValue(property, value);

        var failures = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true));
        Assert.Contains(property, Assert.Single(failures).MemberNames);
    }

    private static void Validate(AuditingOptions options)
    {
        var failures = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true), string.Join("; ", failures));
    }

    private static AuditingOptions WithValue(string property, string value)
    {
        var options = new AuditingOptions();
        typeof(AuditingOptions).GetProperty(property)!.SetValue(options, TimeSpan.Parse(value, CultureInfo.InvariantCulture));

        return options;
    }
}
