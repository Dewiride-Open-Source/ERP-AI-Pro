using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Definitions;

public sealed class JobNamesTests
{
    [Theory]
    [InlineData("identity.users.directory-account-check")]
    [InlineData("test.jobs.race")]
    [InlineData("a.b.c")]
    [InlineData("finance.sales2.invoice-reminders-7d")]
    public void IsWellFormed_ThreeLowerCaseKebabSegments_IsAccepted(string name)
    {
        Assert.True(JobNames.IsWellFormed(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    [InlineData("Identity.users.check")]
    [InlineData("a..c")]
    [InlineData("a.b.c-")]
    [InlineData("a.b.-c")]
    [InlineData("1a.b.c")]
    [InlineData("a.b.c\n")]
    [InlineData("a.b.c d")]
    [InlineData("a_b.c.d")]
    public void IsWellFormed_AnythingElse_IsRefused(string? name)
    {
        Assert.False(JobNames.IsWellFormed(name));
    }

    [Fact]
    public void IsWellFormed_LongerThanTheColumn_IsRefused()
    {
        var name = $"a.b.{new string('c', JobNames.MaxLength - 4)}";

        Assert.True(JobNames.IsWellFormed(name));
        Assert.False(JobNames.IsWellFormed(name + "c"));
    }
}
