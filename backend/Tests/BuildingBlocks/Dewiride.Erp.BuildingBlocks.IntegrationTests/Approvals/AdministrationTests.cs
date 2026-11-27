using Dewiride.Erp.BuildingBlocks.Approvals.Administration;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;
using static Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.ApprovalsDatabase;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals;

public sealed class AdministrationTests
{
    private const string SettingsKey = "platform.approvals.kind-settings";

    [Fact]
    public async Task ListAsync_TheDeclaredKinds_ListsThemByTitleWithTheirSettings()
    {
        await using var database = await CreateAsync();
        var administrator = NewPerson();
        Assert.True((await database.AdministerAsync(administrator, (administration, token) => administration.SetUpAsync(database.FinanceKind.Key, required: false, 4, token))).IsSuccess);

        var kinds = await database.AdministerAsync(administrator, (administration, token) => administration.ListAsync(token));

        Assert.Equal(
            [database.ManagerKind.Key, database.FinanceKind.Key, database.ChainKind.Key, database.UnmappedKind.Key],
            kinds.Select(details => details.Kind.Key));
        var finance = kinds.Single(details => details.Kind == database.FinanceKind);
        Assert.Equal((false, 4), (finance.Setting!.Required, finance.Setting.WorkingDaysToDecide));
        Assert.All(kinds.Where(details => details.Kind != database.FinanceKind), details => Assert.Null(details.Setting));
    }

    [Fact]
    public async Task SetUpAsync_AKind_SavesItsSettingAndRecordsTheChange()
    {
        await using var database = await CreateAsync();
        var administrator = NewPerson();

        var setUp = await database.AdministerAsync(administrator, (administration, token) => administration.SetUpAsync(database.ManagerKind.Key, required: true, 7, token));

        Assert.Equal((database.ManagerKind, true, 7), (setUp.Value.Kind, setUp.Value.Setting!.Required, setUp.Value.Setting.WorkingDaysToDecide));
        var record = Assert.Single(await database.ChangeRecordsOfAsync(setUp.Value.Setting.Id.Value));
        Assert.Equal((ChangeOperation.Created, SettingsKey, administrator), (record.Operation, record.EntityType, record.ActorId));
    }

    [Fact]
    public async Task SetUpAsync_AKindAlreadySetUp_IsRefused()
    {
        await using var database = await CreateAsync();
        var administrator = NewPerson();
        Assert.True((await database.AdministerAsync(administrator, (administration, token) => administration.SetUpAsync(database.ManagerKind.Key, required: true, 2, token))).IsSuccess);

        var again = await database.AdministerAsync(administrator, (administration, token) => administration.SetUpAsync(database.ManagerKind.Key, required: false, 2, token));

        Assert.Equal(("approval.setting-exists", ErrorKind.Conflict), (again.Error!.Code, again.Error.Kind));
    }

    [Fact]
    public async Task SetUpAsync_AKindNoModuleDeclares_IsRefused()
    {
        await using var database = await CreateAsync();

        var refused = await database.AdministerAsync(NewPerson(), (administration, token) => administration.SetUpAsync("test.approvals.undeclared", required: true, 2, token));

        Assert.Equal(("approval.kind-unknown", ErrorKind.Validation), (refused.Error!.Code, refused.Error.Kind));
        Assert.Contains(ApprovalErrors.KindField, refused.Error.Fields.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public async Task SetUpAndChange_WorkingDaysOutsideOneToThirty_AreRefused(int workingDays)
    {
        await using var database = await CreateAsync();
        var administrator = NewPerson();
        var setUp = await database.AdministerAsync(administrator, (administration, token) => administration.SetUpAsync(database.ChainKind.Key, required: true, 2, token));

        var refusedSetUp = await database.AdministerAsync(administrator, (administration, token) => administration.SetUpAsync(database.ManagerKind.Key, required: true, workingDays, token));
        var refusedChange = await database.AdministerAsync(administrator, (administration, token) => ChangeAsync(administration, setUp.Value.Setting!, required: true, workingDays, token));

        Assert.All([refusedSetUp, refusedChange], refused => Assert.Equal("approval.working-days-out-of-range", refused.Error!.Code));
        Assert.Contains(ApprovalErrors.WorkingDaysField, refusedSetUp.Error!.Fields.Keys);
    }

    [Fact]
    public async Task ChangeAsync_FromTheLatestVersion_ChangesTheSettingAndFromAnOlderOneIsRefused()
    {
        await using var database = await CreateAsync();
        var administrator = NewPerson();
        var setUp = await database.AdministerAsync(administrator, (administration, token) => administration.SetUpAsync(database.ManagerKind.Key, required: true, 2, token));
        database.Clock.Advance(TimeSpan.FromMinutes(1));

        var changed = await database.AdministerAsync(administrator, (administration, token) => ChangeAsync(administration, setUp.Value.Setting!, required: false, 3, token));
        var stale = await database.AdministerAsync(administrator, (administration, token) => ChangeAsync(administration, setUp.Value.Setting!, required: true, 9, token));
        var unchanged = await database.AdministerAsync(administrator, (administration, token) => ChangeAsync(administration, changed.Value.Setting!, required: false, 3, token));

        Assert.Equal((false, 3), (changed.Value.Setting!.Required, changed.Value.Setting.WorkingDaysToDecide));
        Assert.NotEqual(setUp.Value.Setting!.Version, changed.Value.Setting.Version);
        Assert.Equal(("concurrency.conflict", ErrorKind.Conflict), (stale.Error!.Code, stale.Error.Kind));
        Assert.NotEqual(changed.Value.Setting.Version, unchanged.Value.Setting!.Version);
        Assert.Equal(
            [ChangeOperation.Created, ChangeOperation.Updated],
            (await database.ChangeRecordsOfAsync(setUp.Value.Setting.Id.Value)).Select(record => record.Operation));
    }

    [Fact]
    public async Task ChangeAsync_ASettingNobodySetUp_IsRefused()
    {
        await using var database = await CreateAsync();
        var missing = new ApprovalKindSettingDetails(ApprovalKindSettingId.Create(), Required: true, 2, RecordVersion.Encode(new byte[8]));

        var refused = await database.AdministerAsync(NewPerson(), (administration, token) => ChangeAsync(administration, missing, required: false, 2, token));

        Assert.Equal(("approval.setting-not-found", ErrorKind.NotFound), (refused.Error!.Code, refused.Error.Kind));
    }

    [Fact]
    public async Task ChangeAsync_TheWorkingDaysOfAKind_LeaveAWaitingRequestWithTheDaysItWasSubmittedWith()
    {
        await using var database = await CreateAsync();
        var (administrator, asker, manager) = (NewPerson(), NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var setUp = await database.AdministerAsync(administrator, (administration, token) => administration.SetUpAsync(database.ChainKind.Key, required: true, 2, token));
        var (_, waiting) = await database.SubmittedClaimAsync(database.ChainKind, asker);
        Assert.True((await database.AdministerAsync(administrator, (administration, token) => ChangeAsync(administration, setUp.Value.Setting!, required: true, 10, token))).IsSuccess);

        Assert.True((await database.DecideAsync(waiting, ApprovalDecision.Approve, Caller(manager))).IsSuccess);
        var (_, later) = await database.SubmittedClaimAsync(database.ChainKind, asker);

        var second = (await database.RequestAsync(waiting)).Steps.Single(step => step.Order == 2);
        Assert.Equal((2, (DateTimeOffset?)IndianStandardTime.StartOfIstDay(new DateOnly(2026, 12, 2))), (second.WorkingDays, second.DueAt));
        Assert.All((await database.RequestAsync(later)).Steps, step => Assert.Equal(10, step.WorkingDays));
    }

    private static Task<Result<ApprovalKindDetails>> ChangeAsync(IApprovalAdministration administration, ApprovalKindSettingDetails setting, bool required, int workingDays, CancellationToken cancellationToken)
    {
        Assert.True(RecordVersion.TryDecode(setting.Version, out var version));

        return administration.ChangeAsync(setting.Id, version, required, workingDays, cancellationToken);
    }
}
