using Dewiride.Erp.BuildingBlocks.Approvals.Administration;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Approvals.UnitTests.Kinds.Application;

internal sealed class RecordingApprovalAdministration : IApprovalAdministration
{
    public static readonly ApprovalKindDetails Answer = new(
        new ApprovalKind("test.approvals.unit-claims", "Unit test claim", "A claim of the unit tests waits for the claimant's manager.", "test.approvals.records", [ApprovalStepDefinition.Manager("Manager")], 2),
        null);

    public List<string> Calls { get; } = [];

    public string? Kind { get; private set; }

    public ApprovalKindSettingId? Id { get; private set; }

    public byte[]? Version { get; private set; }

    public bool? Required { get; private set; }

    public int? WorkingDaysToDecide { get; private set; }

    public Task<IReadOnlyList<ApprovalKindDetails>> ListAsync(CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ListAsync));

        return Task.FromResult<IReadOnlyList<ApprovalKindDetails>>([Answer]);
    }

    public Task<Result<ApprovalKindDetails>> SetUpAsync(string kind, bool required, int workingDaysToDecide, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(SetUpAsync));
        Kind = kind;
        Required = required;
        WorkingDaysToDecide = workingDaysToDecide;

        return Task.FromResult<Result<ApprovalKindDetails>>(Answer);
    }

    public Task<Result<ApprovalKindDetails>> ChangeAsync(ApprovalKindSettingId id, byte[] version, bool required, int workingDaysToDecide, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ChangeAsync));
        Id = id;
        Version = version;
        Required = required;
        WorkingDaysToDecide = workingDaysToDecide;

        return Task.FromResult<Result<ApprovalKindDetails>>(Answer);
    }
}
