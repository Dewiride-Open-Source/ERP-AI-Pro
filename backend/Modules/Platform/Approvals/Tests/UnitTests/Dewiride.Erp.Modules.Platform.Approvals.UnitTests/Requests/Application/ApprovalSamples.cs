using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

namespace Dewiride.Erp.Modules.Platform.Approvals.UnitTests.Requests.Application;

internal static class ApprovalSamples
{
    public static readonly ApprovalRequestSummary Summary = new(
        ApprovalRequestId.Create(),
        "test.approvals.unit-claims",
        "Unit test claim",
        "test.approvals.records",
        Guid.CreateVersion7(),
        null,
        Guid.CreateVersion7(),
        "Meera Nair",
        Guid.CreateVersion7(),
        ApprovalSubmitterKind.Person,
        "Meera Nair",
        new DateTimeOffset(2026, 11, 26, 5, 30, 0, TimeSpan.Zero),
        ApprovalState.Submitted,
        "Manager",
        ApproverKind.Person,
        Guid.CreateVersion7(),
        "Ravi Kumar",
        null,
        new DateTimeOffset(2026, 11, 29, 18, 30, 0, TimeSpan.Zero),
        false,
        null);

    public static readonly ApprovalRequestDetails Details = new(Summary, "AAAAAAAAB9E=", [], [], true, false, false);

    public static readonly PagedResult<ApprovalRequestSummary> Page = new([Summary], 1, PageRequest.DefaultPageSize, 1);

    public static readonly ApprovalCounts Counts = new(3, 1, 2);
}
