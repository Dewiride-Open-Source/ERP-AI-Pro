using Dewiride.Erp.BuildingBlocks.Jobs.Queue;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Queue;

public sealed class JobKeysTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("person:0198f6a2-7c1e-7b3a-9d4e-2f6a8b1c3d5e")]
    [InlineData("invoice.2026-11-01_reminder+1")]
    public void EnsureEnqueueable_KeyOfIdsAndDates_IsAccepted(string key)
    {
        JobKeys.EnsureEnqueueable(key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a b")]
    [InlineData("someone@example.com")]
    [InlineData("café")]
    [InlineData("line\n")]
    public void EnsureEnqueueable_MalformedKey_IsRefused(string? key)
    {
        Assert.Throws<ArgumentException>(() => JobKeys.EnsureEnqueueable(key));
    }

    [Fact]
    public void EnsureEnqueueable_LongerThanTheColumn_IsRefused()
    {
        JobKeys.EnsureEnqueueable(new string('k', JobKeys.MaxLength));

        Assert.Throws<ArgumentException>(() => JobKeys.EnsureEnqueueable(new string('k', JobKeys.MaxLength + 1)));
    }

    [Theory]
    [InlineData("occurrence:2026-11-02T00:30+05:30")]
    [InlineData("Occurrence:anything")]
    public void EnsureEnqueueable_KeyOfTheScheduler_IsRefused(string key)
    {
        Assert.True(JobKeys.IsWellFormed(key));

        var refusal = Assert.Throws<ArgumentException>(() => JobKeys.EnsureEnqueueable(key));

        Assert.Contains("scheduler", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Comparer_KeysThatDifferInCase_AreTheSameKey()
    {
        Assert.True(JobKeys.Comparer.Equals("Person:ABC", "person:abc"));
    }
}
