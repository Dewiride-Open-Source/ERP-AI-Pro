using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Shared;

namespace Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Integration;

public sealed class UsersReportingManagersTests(ApprovalsHost host) : IClassFixture<ApprovalsHost>
{
    [Fact]
    public async Task ManagersOfAsync_APersonWhosePositionNamesAManager_AnswersThatManagerByObjectId()
    {
        using var asker = await host.SignInAsync("Asha Rao");
        using var manager = await host.SignInAsync("Ravi Kumar");
        await host.ReportsToAsync(asker, manager);
        var today = IndianStandardTime.Today(host.Factory.Services.GetRequiredService<TimeProvider>());
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var reportingManagers = scope.ServiceProvider.GetRequiredService<IReportingManagers>();

        var ofAsker = await reportingManagers.ManagersOfAsync(asker.ObjectId, today, TestContext.Current.CancellationToken);
        var ofManager = await reportingManagers.ManagersOfAsync(manager.ObjectId, today, TestContext.Current.CancellationToken);

        Assert.Equal([new ReportingManager(manager.ObjectId, IsActive: true)], ofAsker);
        Assert.Empty(ofManager);
    }
}
