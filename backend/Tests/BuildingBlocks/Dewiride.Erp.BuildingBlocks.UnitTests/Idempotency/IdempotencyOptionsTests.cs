using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Idempotency;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Idempotency;

public sealed class IdempotencyOptionsTests
{
    [Fact]
    public void Validate_Defaults_AreValid()
    {
        var options = new IdempotencyOptions();

        Validate(options);

        Assert.Equal("Erp:Platform:Idempotency", IdempotencyOptions.SectionName);
        Assert.Equal(TimeSpan.FromDays(1), options.RetentionPeriod);
        Assert.Equal(1024 * 1024, options.MaxStoredResponseBytes);
        Assert.Equal(TimeSpan.FromHours(1), options.CleanupInterval);
    }

    [Theory]
    [InlineData(nameof(IdempotencyOptions.RetentionPeriod), "00:05:00")]
    [InlineData(nameof(IdempotencyOptions.RetentionPeriod), "30.00:00:00")]
    [InlineData(nameof(IdempotencyOptions.MaxStoredResponseBytes), "1024")]
    [InlineData(nameof(IdempotencyOptions.MaxStoredResponseBytes), "16777216")]
    [InlineData(nameof(IdempotencyOptions.CleanupInterval), "00:01:00")]
    [InlineData(nameof(IdempotencyOptions.CleanupInterval), "1.00:00:00")]
    public void Validate_ValueOnTheBound_Passes(string property, string value)
    {
        Validate(WithValue(property, value));
    }

    [Theory]
    [InlineData(nameof(IdempotencyOptions.RetentionPeriod), "00:04:59")]
    [InlineData(nameof(IdempotencyOptions.RetentionPeriod), "31.00:00:00")]
    [InlineData(nameof(IdempotencyOptions.MaxStoredResponseBytes), "1023")]
    [InlineData(nameof(IdempotencyOptions.MaxStoredResponseBytes), "16777217")]
    [InlineData(nameof(IdempotencyOptions.CleanupInterval), "00:00:59")]
    [InlineData(nameof(IdempotencyOptions.CleanupInterval), "1.00:00:01")]
    public void Validate_ValueOutsideTheBound_Fails(string property, string value)
    {
        var options = WithValue(property, value);

        var failures = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true));
        Assert.Contains(property, Assert.Single(failures).MemberNames);
    }

    private static void Validate(IdempotencyOptions options)
    {
        var failures = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true), string.Join("; ", failures));
    }

    private static IdempotencyOptions WithValue(string property, string value)
    {
        var options = new IdempotencyOptions();
        var info = typeof(IdempotencyOptions).GetProperty(property)!;
        info.SetValue(options, info.PropertyType == typeof(TimeSpan) ? TimeSpan.Parse(value, CultureInfo.InvariantCulture) : int.Parse(value, CultureInfo.InvariantCulture));

        return options;
    }
}
