using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints.Requests;

// The box is bound as text: a query value of an enum type is read by the member names as written, while the document would
// describe it by the camelCase names that JSON carries, so the generated client would send values the binding refuses.
public sealed record ListApprovalRequestsRequest(
    [property: FromQuery(Name = "box")]
    [property: Description("Which requests to list: waiting, those that wait for your decision, late first, when absent; mine, those you asked for or submitted; decided, those with a step you decided; all, every request, which needs the permission platform.approvals.requests.read-all.")]
    [property: RegularExpression(ListApprovalRequestsRequest.BoxPattern)]
    string? Box)
{
    public const string BoxPattern = "^(waiting|mine|decided|all)$";

    internal ApprovalBox Selected => Box switch
    {
        "mine" => ApprovalBox.Mine,
        "decided" => ApprovalBox.Decided,
        "all" => ApprovalBox.All,
        _ => ApprovalBox.Waiting,
    };
}
