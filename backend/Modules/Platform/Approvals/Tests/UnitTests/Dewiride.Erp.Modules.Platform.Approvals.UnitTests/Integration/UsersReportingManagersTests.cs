using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.Modules.Identity.Users.Contracts.Positions;
using Dewiride.Erp.Modules.Platform.Approvals.Integration;

namespace Dewiride.Erp.Modules.Platform.Approvals.UnitTests.Integration;

public sealed class UsersReportingManagersTests
{
    private static readonly DateOnly On = new(2026, 11, 27);

    [Fact]
    public async Task ManagersOfAsync_ThePeopleTheUsersModuleAnswers_AreTheManagersByObjectIdAndStateNearestFirst()
    {
        var nearest = Guid.CreateVersion7();
        var top = Guid.CreateVersion7();
        var lines = new ScriptedReportingLines(
            new ReportingManagerEntry(Guid.CreateVersion7(), nearest, IsActive: false),
            new ReportingManagerEntry(Guid.CreateVersion7(), null, IsActive: true),
            new ReportingManagerEntry(Guid.CreateVersion7(), top, IsActive: true));
        var person = Guid.CreateVersion7();

        var managers = await new UsersReportingManagers(lines).ManagersOfAsync(person, On, TestContext.Current.CancellationToken);

        Assert.Equal([new ReportingManager(nearest, IsActive: false), new ReportingManager(null, IsActive: true), new ReportingManager(top, IsActive: true)], managers);
        Assert.Equal([(person, On)], lines.Asked);
    }

    [Fact]
    public async Task ManagersOfAsync_APersonTheUsersModuleKnowsNoManagerOf_HasNone()
    {
        var lines = new ScriptedReportingLines();

        var managers = await new UsersReportingManagers(lines).ManagersOfAsync(Guid.CreateVersion7(), On, TestContext.Current.CancellationToken);

        Assert.Empty(managers);
    }
}
