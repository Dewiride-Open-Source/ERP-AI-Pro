using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Definitions;

public sealed class JobRetryPolicyTests
{
    [Theory]
    [InlineData(5, 1, 60, 1, 1)]
    [InlineData(5, 1, 60, 2, 2)]
    [InlineData(5, 1, 60, 3, 4)]
    [InlineData(5, 1, 60, 6, 32)]
    [InlineData(5, 1, 60, 7, 60)]
    [InlineData(5, 1, 60, 63, 60)]
    [InlineData(5, 1, 60, 64, 60)]
    [InlineData(5, 1, 60, int.MaxValue, 60)]
    [InlineData(6, 5, 60, 1, 5)]
    [InlineData(6, 5, 60, 2, 10)]
    [InlineData(6, 5, 60, 3, 20)]
    [InlineData(6, 5, 60, 4, 40)]
    [InlineData(6, 5, 60, 5, 60)]
    [InlineData(20, 1440, 10080, 3, 5760)]
    [InlineData(20, 1440, 10080, 4, 10080)]
    public void DelayAfter_Failures_DoubleUntilTheCap(int maxAttempts, int firstMinutes, int maxMinutes, int failures, int expectedMinutes)
    {
        var policy = new JobRetryPolicy(maxAttempts, TimeSpan.FromMinutes(firstMinutes), TimeSpan.FromMinutes(maxMinutes));

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), policy.DelayAfter(failures));
    }

    [Fact]
    public void DelayAfter_NoFailure_IsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => JobRetryPolicy.Default.DelayAfter(0));
    }

    [Fact]
    public void Default_FiveAttempts_WaitsAMinuteThenDoublesToAnHour()
    {
        Assert.Equal(new JobRetryPolicy(5, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1)), JobRetryPolicy.Default);
    }

    [Theory]
    [InlineData(1, "00:00:01", "00:00:01")]
    [InlineData(20, "1.00:00:00", "7.00:00:00")]
    [InlineData(6, "00:05:00", "01:00:00")]
    public void EnsureValid_PolicyOnTheBounds_Passes(int maxAttempts, string firstDelay, string maxDelay)
    {
        var policy = new JobRetryPolicy(maxAttempts, TimeSpan.Parse(firstDelay, CultureInfo.InvariantCulture), TimeSpan.Parse(maxDelay, CultureInfo.InvariantCulture));

        policy.EnsureValid("test.jobs.retry");
    }

    [Theory]
    [InlineData(0, "00:01:00", "01:00:00")]
    [InlineData(21, "00:01:00", "01:00:00")]
    [InlineData(5, "00:00:00.999", "01:00:00")]
    [InlineData(5, "1.00:00:01", "2.00:00:00")]
    [InlineData(5, "00:10:00", "00:09:59")]
    [InlineData(5, "00:01:00", "7.00:00:01")]
    public void EnsureValid_PolicyOutsideTheBounds_IsRefused(int maxAttempts, string firstDelay, string maxDelay)
    {
        var policy = new JobRetryPolicy(maxAttempts, TimeSpan.Parse(firstDelay, CultureInfo.InvariantCulture), TimeSpan.Parse(maxDelay, CultureInfo.InvariantCulture));

        var refusal = Assert.Throws<ArgumentException>(() => policy.EnsureValid("test.jobs.retry"));

        Assert.Contains("test.jobs.retry", refusal.Message, StringComparison.Ordinal);
    }
}
