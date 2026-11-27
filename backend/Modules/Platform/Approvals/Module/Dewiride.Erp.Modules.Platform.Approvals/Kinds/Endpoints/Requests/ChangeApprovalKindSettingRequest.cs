using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;

namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Endpoints.Requests;

public sealed record ChangeApprovalKindSettingRequest(
    [property: Description("Whether the kind's records need approval from now on; false lets each go through at once, without a request, so nobody but the person who made it looks at it.")]
    [property: Required]
    bool? Required,
    [property: Description("Working days each step of the requests submitted from now on has, from 1 to 30; a request already waiting keeps its due times.")]
    [property: Required]
    [property: Range(ApprovalKind.MinWorkingDays, ApprovalKind.MaxWorkingDays)]
    int? WorkingDaysToDecide,
    [property: Description("Version of the setting the change was made from, as the API last returned it.")]
    [property: Required]
    [property: StringLength(RecordVersion.InputMaxLength)]
    string? Version);
