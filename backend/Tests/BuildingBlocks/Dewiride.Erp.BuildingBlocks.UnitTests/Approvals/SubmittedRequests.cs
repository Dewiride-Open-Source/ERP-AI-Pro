using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Approvals;

// A request as its submission leaves it once saved: the person who submitted it is stamped as the auditing interceptor stamps
// it, through a context that never opens a connection.
internal static class SubmittedRequests
{
    public const string Kind = "test.approvals.claims";

    public const int WorkingDays = 2;

    public static ApprovalStepPlan ManagerStep(Guid manager) => ApprovalStepPlan.ForPerson(ApprovalStepDefinition.Manager("Manager"), manager, WorkingDays);

    public static ApprovalStepPlan RoleStep(string title, ErpRole role) => ApprovalStepPlan.ForRole(ApprovalStepDefinition.HeldBy(title, role), role, WorkingDays);

    public static ApprovalRequest Of(Guid requestedFor, Guid submittedBy, DateTimeOffset submittedAt, params ApprovalStepPlan[] steps)
    {
        var request = ApprovalRequest.Submit(Kind, Kind, Guid.CreateVersion7(), requestedFor, steps, submittedAt);
        using var context = new ApprovalsDbContext(new DbContextOptionsBuilder<ApprovalsDbContext>().UseSqlServer().Options);
        context.Requests.Add(request);
        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, submittedAt, submittedBy);

        return request;
    }
}
