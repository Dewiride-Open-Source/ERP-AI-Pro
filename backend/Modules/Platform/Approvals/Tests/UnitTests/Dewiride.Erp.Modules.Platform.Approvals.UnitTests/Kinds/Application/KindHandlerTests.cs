using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Commands.ChangeApprovalKindSetting;
using Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Commands.SetUpApprovalKindSetting;
using Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Queries.ListApprovalKinds;

namespace Dewiride.Erp.Modules.Platform.Approvals.UnitTests.Kinds.Application;

public sealed class KindHandlerTests
{
    [Fact]
    public async Task List_Always_AnswersTheAdministrationsKinds()
    {
        var administration = new RecordingApprovalAdministration();

        var result = await new ListApprovalKindsHandler(administration).HandleAsync(new ListApprovalKindsQuery(), TestContext.Current.CancellationToken);

        Assert.Equal([RecordingApprovalAdministration.Answer], result.Value);
        Assert.Equal(["ListAsync"], administration.Calls);
    }

    [Fact]
    public async Task SetUp_ACommand_PassesItsKindWhetherItNeedsApprovalAndItsWorkingDays()
    {
        var administration = new RecordingApprovalAdministration();

        var result = await new SetUpApprovalKindSettingHandler(administration).HandleAsync(new SetUpApprovalKindSettingCommand("test.approvals.unit-claims", false, 5), TestContext.Current.CancellationToken);

        Assert.Same(RecordingApprovalAdministration.Answer, result.Value);
        Assert.Equal(["SetUpAsync"], administration.Calls);
        Assert.Equal(("test.approvals.unit-claims", (bool?)false, (int?)5), (administration.Kind, administration.Required, administration.WorkingDaysToDecide));
    }

    [Fact]
    public async Task Change_AVersionTheApiGave_PassesItsBytesWithTheChange()
    {
        var administration = new RecordingApprovalAdministration();
        var id = ApprovalKindSettingId.Create();
        byte[] version = [0, 0, 0, 0, 0, 0, 7, 209];

        var result = await new ChangeApprovalKindSettingHandler(administration).HandleAsync(new ChangeApprovalKindSettingCommand(id, true, 3, Convert.ToBase64String(version)), TestContext.Current.CancellationToken);

        Assert.Same(RecordingApprovalAdministration.Answer, result.Value);
        Assert.Equal(["ChangeAsync"], administration.Calls);
        Assert.Equal((id, (bool?)true, (int?)3), (administration.Id, administration.Required, administration.WorkingDaysToDecide));
        Assert.Equal(version, administration.Version);
    }

    [Theory]
    [InlineData("not base64")]
    [InlineData("AAAA")]
    public async Task Change_AVersionTheApiNeverGave_IsRefusedWithoutAChange(string version)
    {
        var administration = new RecordingApprovalAdministration();

        var result = await new ChangeApprovalKindSettingHandler(administration).HandleAsync(new ChangeApprovalKindSettingCommand(ApprovalKindSettingId.Create(), true, 3, version), TestContext.Current.CancellationToken);

        Assert.Equal(ApprovalErrors.VersionInvalid.Code, result.Error!.Code);
        Assert.Contains(ApprovalErrors.VersionField, result.Error.Fields.Keys);
        Assert.Empty(administration.Calls);
    }
}
