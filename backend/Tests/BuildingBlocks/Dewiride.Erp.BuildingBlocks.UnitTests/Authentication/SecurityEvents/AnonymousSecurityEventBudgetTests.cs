using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.SecurityEvents;

public sealed class AnonymousSecurityEventBudgetTests
{
    private readonly FakeLogger<AnonymousSecurityEventBudget> _logs = new();

    private readonly FakeTimeProvider _clock = new();

    [Fact]
    public void TryTake_EventsWithinTheBudget_TakesEveryOneAndLogsNothing()
    {
        var budget = Budget();

        for (var i = 0; i < AnonymousSecurityEventBudget.EventsPerWindow; i++)
        {
            Assert.True(budget.TryTake());
        }

        Assert.Equal(0, _logs.Collector.Count);
    }

    [Fact]
    public void TryTake_EventsOverTheBudget_AreRefusedAndTheLimitIsLoggedOncePerWindow()
    {
        var budget = Budget();
        Spend(budget);

        Assert.False(budget.TryTake());
        Assert.False(budget.TryTake());
        Assert.False(budget.TryTake());

        var record = Assert.Single(_logs.Collector.GetSnapshot());
        Assert.Equal("60", record.GetStructuredStateValue("Limit"));
    }

    [Fact]
    public void TryTake_FirstEventOfALaterWindow_IsTakenAndPrecededByHowManyWereHeldBack()
    {
        var budget = Budget();
        Spend(budget);
        budget.TryTake();
        budget.TryTake();

        _clock.Advance(AnonymousSecurityEventBudget.Window);

        Assert.True(budget.TryTake());
        Assert.Equal("2", _logs.LatestRecord.GetStructuredStateValue("Count"));
    }

    [Fact]
    public void TryTake_LimitReachedAgainInALaterWindow_IsLoggedAgain()
    {
        var budget = Budget();
        Spend(budget);
        budget.TryTake();
        _clock.Advance(AnonymousSecurityEventBudget.Window);
        Spend(budget);

        Assert.False(budget.TryTake());

        Assert.Equal(2, _logs.Collector.GetSnapshot().Count(record => record.GetStructuredStateValue("Limit") is not null));
    }

    [Fact]
    public void TryTake_WindowNotYetOver_KeepsRefusing()
    {
        var budget = Budget();
        Spend(budget);

        _clock.Advance(AnonymousSecurityEventBudget.Window - TimeSpan.FromTicks(1));

        Assert.False(budget.TryTake());
    }

    private AnonymousSecurityEventBudget Budget() => new(_clock, _logs);

    private static void Spend(AnonymousSecurityEventBudget budget)
    {
        for (var i = 0; i < AnonymousSecurityEventBudget.EventsPerWindow; i++)
        {
            budget.TryTake();
        }
    }
}
