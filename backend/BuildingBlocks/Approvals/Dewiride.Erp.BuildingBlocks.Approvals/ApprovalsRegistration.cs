using Dewiride.Erp.BuildingBlocks.Approvals.Administration;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Approvals.Submitting;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Approvals;

// The reporting lines a manager step needs are a module's, so the module that holds them registers IReportingManagers, and the
// routing asks for it only when a step goes to a manager.
public static class ApprovalsRegistration
{
    public static IHostApplicationBuilder AddErpApprovals(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<ApprovalsDbContext>(ApprovalsDbContext.SchemaName);
        builder.Services.TryAddSingleton(provider => new ApprovalKindCatalog(provider.GetServices<ApprovalKindRegistration>()));
        builder.Services.TryAddScoped<ApprovalRouting>();
        builder.Services.TryAddScoped<IApprovals, RecordApprovals>();
        builder.Services.TryAddScoped<IApprovalInbox, ApprovalInbox>();
        builder.Services.TryAddScoped<IApprovalDecisions, ApprovalDecisions>();
        builder.Services.TryAddScoped<IApprovalAdministration, ApprovalAdministration>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecordNames, ApprovalRecordNames>());

        return builder;
    }
}
