using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Running;

public sealed class JobRunnerOptionsTests
{
    [Fact]
    public void Validate_Defaults_AreValid()
    {
        var options = new JobRunnerOptions();

        Validate(options);

        Assert.Equal("Erp:Platform:Jobs", JobRunnerOptions.SectionName);
        Assert.Equal(TimeSpan.FromSeconds(30), options.PollInterval);
        Assert.Equal(TimeSpan.FromMinutes(2), options.LeaseDuration);
        Assert.Equal(TimeSpan.FromSeconds(40), options.RenewalInterval);
        Assert.Equal(2, options.MaxConcurrentRuns);
    }

    [Theory]
    [InlineData(nameof(JobRunnerOptions.PollInterval), "00:00:01")]
    [InlineData(nameof(JobRunnerOptions.PollInterval), "00:10:00")]
    [InlineData(nameof(JobRunnerOptions.LeaseDuration), "00:00:30")]
    [InlineData(nameof(JobRunnerOptions.LeaseDuration), "01:00:00")]
    [InlineData(nameof(JobRunnerOptions.MaxConcurrentRuns), "1")]
    [InlineData(nameof(JobRunnerOptions.MaxConcurrentRuns), "16")]
    public void Validate_ValueOnTheBound_Passes(string property, string value)
    {
        Validate(WithValue(property, value));
    }

    [Theory]
    [InlineData(nameof(JobRunnerOptions.PollInterval), "00:00:00.999")]
    [InlineData(nameof(JobRunnerOptions.PollInterval), "00:10:01")]
    [InlineData(nameof(JobRunnerOptions.LeaseDuration), "00:00:29")]
    [InlineData(nameof(JobRunnerOptions.LeaseDuration), "01:00:01")]
    [InlineData(nameof(JobRunnerOptions.MaxConcurrentRuns), "0")]
    [InlineData(nameof(JobRunnerOptions.MaxConcurrentRuns), "17")]
    public void Validate_ValueOutsideTheBound_Fails(string property, string value)
    {
        var options = WithValue(property, value);

        var failures = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true));
        Assert.Contains(property, Assert.Single(failures).MemberNames);
    }

    private static void Validate(JobRunnerOptions options)
    {
        var failures = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true), string.Join("; ", failures));
    }

    private static JobRunnerOptions WithValue(string property, string value)
    {
        var options = new JobRunnerOptions();
        var info = typeof(JobRunnerOptions).GetProperty(property)!;
        info.SetValue(options, info.PropertyType == typeof(TimeSpan) ? TimeSpan.Parse(value, CultureInfo.InvariantCulture) : int.Parse(value, CultureInfo.InvariantCulture));

        return options;
    }
}
