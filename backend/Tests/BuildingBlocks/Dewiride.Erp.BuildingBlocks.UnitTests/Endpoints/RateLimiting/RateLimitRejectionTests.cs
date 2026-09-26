using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.RateLimiting;

public sealed class RateLimitRejectionTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(200, 1)]
    [InlineData(1000, 1)]
    [InlineData(1001, 2)]
    [InlineData(60_000, 60)]
    public void RetryAfterSeconds_Estimate_RoundsUpToWholeSecondsOfAtLeastOne(int milliseconds, long seconds)
    {
        Assert.Equal(seconds, RateLimitRejection.RetryAfterSeconds(TimeSpan.FromMilliseconds(milliseconds)));
    }
}
