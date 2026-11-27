using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;

namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Endpoints.Requests;

public sealed record SetUpApprovalKindSettingRequest(
    [property: Description("Key of the kind of request, as the list of kinds gives it.")]
    [property: Required]
    [property: StringLength(ApprovalKind.KeyMaxLength)]
    string? Kind,
    [property: Description("Whether the kind's records need approval; false lets each go through at once, without a request, so nobody but the person who made it looks at it.")]
    [property: Required]
    bool? Required,
    [property: Description("Working days each step of the kind's requests has, from 1 to 30.")]
    [property: Required]
    [property: Range(ApprovalKind.MinWorkingDays, ApprovalKind.MaxWorkingDays)]
    int? WorkingDaysToDecide);
