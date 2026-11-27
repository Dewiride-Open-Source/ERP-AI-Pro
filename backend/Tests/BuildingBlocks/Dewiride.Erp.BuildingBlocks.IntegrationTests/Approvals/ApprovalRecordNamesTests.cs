using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using static Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.ApprovalsDatabase;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals;

public sealed class ApprovalRecordNamesTests
{
    private const string RequestsKey = "platform.approvals.requests";

    private const string StepsKey = "platform.approvals.steps";

    private const string SettingsKey = "platform.approvals.kind-settings";

    [Fact]
    public async Task NamesAsync_ARequestAStepAndASetting_AreNamedByTheirKindAsThisReleaseTitlesIt()
    {
        await using var database = await CreateAsync();
        var administrator = NewPerson();
        database.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 27, 19, 0, 0, TimeSpan.Zero));
        var (_, requestId) = await database.SubmittedClaimAsync(database.ChainKind, NewPerson());
        var stepId = (await database.RequestAsync(requestId)).Steps.Single(step => step.Order == 2).Id;
        var setting = (await database.AdministerAsync(administrator, (administration, token) => administration.SetUpAsync(database.FinanceKind.Key, required: true, 3, token))).Value.Setting!;

        var names = await database.AsAsync(administrator, async (services, token) =>
        {
            var recordNames = services.GetServices<IRecordNames>().Single(names => names.EntityTypes.Contains(RequestsKey));

            return (
                Request: await recordNames.NamesAsync(RequestsKey, [requestId.Value, Guid.CreateVersion7()], token),
                Step: await recordNames.NamesAsync(StepsKey, [stepId.Value], token),
                Setting: await recordNames.NamesAsync(SettingsKey, [setting.Id.Value], token),
                recordNames.EntityTypes);
        });

        Assert.Equal(new Dictionary<Guid, string> { [requestId.Value] = "Claim approval by manager and Finance, 28-11-2026" }, names.Request);
        Assert.Equal(new Dictionary<Guid, string> { [stepId.Value] = "Finance of Claim approval by manager and Finance" }, names.Step);
        Assert.Equal(new Dictionary<Guid, string> { [setting.Id.Value] = "Claim approval by Finance" }, names.Setting);
        Assert.Equal(new HashSet<string>([RequestsKey, StepsKey, SettingsKey]), names.EntityTypes);
    }

    [Fact]
    public async Task NamesAsync_AKeyItDoesNotName_Throws()
    {
        await using var database = await CreateAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => database.AsAsync(NewPerson(), (services, token) =>
            services.GetServices<IRecordNames>().Single(names => names.EntityTypes.Contains(RequestsKey)).NamesAsync("platform.approvals.unknown", [Guid.CreateVersion7()], token)));
    }
}
