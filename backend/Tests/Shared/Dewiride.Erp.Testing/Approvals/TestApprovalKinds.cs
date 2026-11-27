using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Testing.Approvals;

// Kinds of approval no release declares, so that a test host asks for approval before any module does. They approve no stored
// record, so their requests are saved in the approvals' own context. Every test of a process or of an end-to-end run writes into
// one database, a request routed to a role waits for every holder of that role, and a kind's setting is the whole kind's, so each
// test that reads a list, a count or a setting takes a kind no other test running beside it takes: every scenario has twelve slots,
// which the end-to-end runs share out by project, repetition and theme, and the backend's tests take the API kinds. No test decides
// a request of the untouched kind or sets it up. The record of a refused kind refuses to be approved and accepts every other
// outcome; the records of every other kind accept them all.
public static class TestApprovalKinds
{
    public const string SubjectType = "test.approvals.records";

    public const int Slots = 12;

    public const int WorkingDaysToDecide = 2;

    public const string ManagerStepTitle = "Manager";

    public const string FinanceStepTitle = "Finance";

    private static readonly ApprovalStepDefinition ManagerStep = ApprovalStepDefinition.Manager(ManagerStepTitle);

    private static readonly ApprovalStepDefinition FinanceStep = ApprovalStepDefinition.HeldBy(FinanceStepTitle, ErpRole.Finance);

    private static readonly IReadOnlyList<ApprovalKind> ManagerKinds =
        Slotted("manager", "Manager test approval", "A record of the tests waits for the manager of the person it is for.", ManagerStep);

    private static readonly IReadOnlyList<ApprovalKind> ChainKinds =
        Slotted("chain", "Manager and Finance test approval", "A record of the tests waits for the manager of the person it is for and then for Finance.", ManagerStep, FinanceStep);

    private static readonly IReadOnlyList<ApprovalKind> FinanceKinds =
        Slotted("finance", "Finance test approval", "A record of the tests waits for Finance.", FinanceStep);

    private static readonly IReadOnlyList<ApprovalKind> NoManagerKinds =
        Slotted("nomanager", "No-manager test approval", "A record of the tests waits for the manager of a person who has none, so for the administrators.", ManagerStep);

    private static readonly IReadOnlyList<ApprovalKind> RefusedKinds =
        Slotted("refused", "Refused test approval", "A record of the tests waits for the manager of the person it is for and refuses to be approved.", ManagerStep);

    private static readonly IReadOnlyList<ApprovalKind> SettingsKinds =
        Slotted("settings", "Settings test approval", "A record of the tests waits for the manager of the person it is for, as the administrators set its kind up.", ManagerStep);

    private static readonly IReadOnlyList<ApprovalKind> ApiKinds =
        Slotted("api", "API test approval", "A record of the backend's tests waits for the manager of the person it is for and then for Finance.", ManagerStep, FinanceStep);

    private static readonly ApprovalKind UntouchedKind =
        Create("untouched", 1, "Untouched test approval", "A record of the tests waits for the manager of the person it is for, and no test changes its kind.", ManagerStep);

    private static readonly IReadOnlyList<ApprovalKind> Kinds =
        [.. ManagerKinds, .. ChainKinds, .. FinanceKinds, .. NoManagerKinds, .. RefusedKinds, .. SettingsKinds, UntouchedKind, .. ApiKinds];

    private static readonly Dictionary<string, ApprovalKind> ByKey = Kinds.ToDictionary(kind => kind.Key, StringComparer.Ordinal);

    public static IReadOnlyList<ApprovalKind> All => Kinds;

    public static ApprovalKind Untouched => UntouchedKind;

    public static ApprovalKind Manager(int slot) => Slot(ManagerKinds, slot);

    public static ApprovalKind Chain(int slot) => Slot(ChainKinds, slot);

    public static ApprovalKind Finance(int slot) => Slot(FinanceKinds, slot);

    public static ApprovalKind NoManager(int slot) => Slot(NoManagerKinds, slot);

    public static ApprovalKind Refused(int slot) => Slot(RefusedKinds, slot);

    public static ApprovalKind Settings(int slot) => Slot(SettingsKinds, slot);

    public static ApprovalKind Api(int slot) => Slot(ApiKinds, slot);

    public static ApprovalKind? Find(string? key) => key is null ? null : ByKey.GetValueOrDefault(key);

    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var kind in Kinds)
        {
            if (RefusedKinds.Contains(kind))
            {
                services.AddApprovalKind<ApprovalsDbContext, RefusingApprovalOutcomes>(kind);
            }
            else
            {
                services.AddApprovalKind<ApprovalsDbContext, TestApprovalOutcomes>(kind);
            }
        }
    }

    private static ApprovalKind Slot(IReadOnlyList<ApprovalKind> kinds, int slot)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(slot, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(slot, Slots);

        return kinds[slot - 1];
    }

    private static ApprovalKind[] Slotted(string scenario, string title, string description, params ApprovalStepDefinition[] steps) =>
        [.. Enumerable.Range(1, Slots).Select(slot => Create(scenario, slot, title, description, steps))];

    private static ApprovalKind Create(string scenario, int slot, string title, string description, params ApprovalStepDefinition[] steps)
    {
        var suffix = slot.ToString("00", CultureInfo.InvariantCulture);

        return new ApprovalKind($"test.approvals.{scenario}-{suffix}", $"{title} {suffix}", description, SubjectType, [.. steps], WorkingDaysToDecide);
    }
}
