using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Auditing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Auditing;

public sealed class AuditingOptionsTests
{
    [Fact]
    public void Validate_Defaults_AreValidKeepSecurityEventsForAYearAndEveryChangeRecord()
    {
        var options = new AuditingOptions();

        Validate(options);

        Assert.Equal("Erp:Platform:Auditing", AuditingOptions.SectionName);
        Assert.Equal(TimeSpan.FromDays(365), options.SecurityEventRetention);
        Assert.Null(options.ChangeRecordRetentionYears);
    }

    [Theory]
    [InlineData("1.00:00:00")]
    [InlineData("3650.00:00:00")]
    public void Validate_SecurityEventRetentionOnTheBound_Passes(string value)
    {
        Validate(new AuditingOptions { SecurityEventRetention = TimeSpan.Parse(value, CultureInfo.InvariantCulture) });
    }

    [Theory]
    [InlineData("23:59:59")]
    [InlineData("3650.00:00:01")]
    public void Validate_SecurityEventRetentionOutsideTheBound_Fails(string value)
    {
        AssertFails(new AuditingOptions { SecurityEventRetention = TimeSpan.Parse(value, CultureInfo.InvariantCulture) }, nameof(AuditingOptions.SecurityEventRetention));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public void Validate_ChangeRecordRetentionYearsOnTheBound_Passes(int years)
    {
        Validate(new AuditingOptions { ChangeRecordRetentionYears = years });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(-8)]
    public void Validate_ChangeRecordRetentionYearsOutsideTheBound_Fails(int years)
    {
        AssertFails(new AuditingOptions { ChangeRecordRetentionYears = years }, nameof(AuditingOptions.ChangeRecordRetentionYears));
    }

    private static void Validate(AuditingOptions options)
    {
        var failures = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true), string.Join("; ", failures));
    }

    private static void AssertFails(AuditingOptions options, string property)
    {
        var failures = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true));
        Assert.Contains(property, Assert.Single(failures).MemberNames);
    }
}
