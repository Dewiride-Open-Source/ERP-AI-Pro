using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.UnitTests.Positions.Domain;

public sealed class ReportingLinesTests
{
    private static readonly DateOnly January = new(2026, 1, 1);

    private static readonly DateOnly April = new(2026, 4, 1);

    private static readonly DateOnly July = new(2026, 7, 1);

    private static readonly DateOnly October = new(2026, 10, 1);

    private readonly UserId _asha = UserId.Create();

    private readonly UserId _dev = UserId.Create();

    private readonly UserId _meera = UserId.Create();

    [Fact]
    public void ManagerOf_DateBetweenTwoPositions_IsTheManagerOfTheEarlierOne()
    {
        var lines = new ReportingLines([new(_asha, January, _dev), new(_asha, July, _meera)]);

        Assert.Equal(_dev, lines.ManagerOf(_asha, April));
        Assert.Equal(_meera, lines.ManagerOf(_asha, July));
        Assert.Equal(_meera, lines.ManagerOf(_asha, October));
    }

    [Fact]
    public void ManagerOf_DateBeforeThePersonsFirstPosition_IsNone()
    {
        var lines = new ReportingLines([new(_asha, April, _dev)]);

        Assert.Null(lines.ManagerOf(_asha, January));
        Assert.Null(lines.ManagerOf(_meera, April));
    }

    [Fact]
    public void ClosesCycle_ManagerWhoReportsToThePersonOnTheDate_IsTrue()
    {
        var lines = new ReportingLines([new(_dev, January, _asha), new(_asha, April, _dev)]);

        Assert.True(lines.ClosesCycle(_asha, April, null));
    }

    [Fact]
    public void ClosesCycle_LongerChainBackToThePerson_IsTrue()
    {
        var lines = new ReportingLines([new(_dev, January, _meera), new(_meera, January, _asha), new(_asha, April, _dev)]);

        Assert.True(lines.ClosesCycle(_asha, April, null));
    }

    [Fact]
    public void ClosesCycle_ChainThatReachesThePersonOnlyFromALaterDateTheChangeStillHolds_IsTrue()
    {
        var lines = new ReportingLines([new(_asha, April, _dev), new(_dev, October, _asha)]);

        Assert.True(lines.ClosesCycle(_asha, April, null));
    }

    [Fact]
    public void ClosesCycle_CycleThatOnlyThePersonsNextPositionCloses_IsLeftToThatPosition()
    {
        var lines = new ReportingLines([new(_asha, April, _dev), new(_asha, July, _meera), new(_meera, January, _asha)]);

        Assert.False(lines.ClosesCycle(_asha, April, July));
        Assert.True(lines.ClosesCycle(_asha, April, null));
    }

    [Fact]
    public void ClosesCycle_ChainThatReachedThePersonOnlyBeforeTheChange_IsFalse()
    {
        var lines = new ReportingLines([new(_dev, January, _asha), new(_dev, April, null), new(_asha, July, _dev)]);

        Assert.False(lines.ClosesCycle(_asha, July, null));
    }

    [Fact]
    public void ClosesCycle_ManagersThatEndWithoutReachingThePerson_IsFalse()
    {
        var lines = new ReportingLines([new(_asha, April, _dev), new(_dev, January, _meera)]);

        Assert.False(lines.ClosesCycle(_asha, April, null));
    }

    [Fact]
    public void ClosesCycle_LoopAmongOthersThatNeverReachesThePerson_StopsAndIsFalse()
    {
        var lines = new ReportingLines([new(_dev, January, _meera), new(_meera, January, _dev), new(_asha, April, _dev)]);

        Assert.False(lines.ClosesCycle(_asha, April, null));
    }
}
