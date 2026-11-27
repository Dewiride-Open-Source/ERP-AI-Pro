using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.Approvals.Contracts;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Commands.DecideApprovalRequest;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Commands.WithdrawApprovalRequest;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.CountApprovalRequests;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.GetApprovalRequest;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.ListApprovalRequests;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints.Requests;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints;

// A decision and a withdrawal name the version of the request they were made from, so none takes an idempotency key: repeated, it
// finds the request closed or at a newer version and is refused. Which requests a caller sees, decides and withdraws is the building
// block's to settle, from the roles the request carries and whether it holds the permissions to see and decide every request.
internal static class RequestEndpoints
{
    public const string ListRouteName = "Platform.Approvals.ListApprovalRequests";

    public const string CountRouteName = "Platform.Approvals.CountApprovalRequests";

    public const string GetRouteName = "Platform.Approvals.GetApprovalRequest";

    public const string ApproveRouteName = "Platform.Approvals.ApproveApprovalRequest";

    public const string ReturnRouteName = "Platform.Approvals.ReturnApprovalRequest";

    public const string RejectRouteName = "Platform.Approvals.RejectApprovalRequest";

    public const string WithdrawRouteName = "Platform.Approvals.WithdrawApprovalRequest";

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/requests", ListAsync)
            .WithName(ListRouteName)
            .WithSummary("Lists the approval requests of one box: those that wait for the caller's decision, late first; those the caller asked for or submitted; those with a step the caller decided; or every request, for a caller who may see them all.")
            .RequirePermission(ApprovalsPermissions.Read);

        group.MapGet("/requests/counts", CountAsync)
            .WithName(CountRouteName)
            .WithSummary("Counts the requests that wait for the caller's decision, how many of them are late, and how many the caller asked for or submitted still wait.")
            .RequirePermission(ApprovalsPermissions.Read);

        group.MapGet("/requests/{id:guid}", GetAsync)
            .WithName(GetRouteName)
            .WithSummary("Describes one approval request with its steps, the earlier requests of its record and what the caller may do with it; a request the caller may not see answers as an unknown one.")
            .RequirePermission(ApprovalsPermissions.Read);

        group.MapPost("/requests/{id:guid}/approve", ApproveAsync)
            .WithName(ApproveRouteName)
            .WithSummary("Approves the step a request waits at: its next step starts waiting, or at the last step the request is approved together with the change its module makes to the record.")
            .RequirePermission(ApprovalsPermissions.Decide)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/requests/{id:guid}/return", ReturnAsync)
            .WithName(ReturnRouteName)
            .WithSummary("Sends a request back with what needs to change, closing it together with the change its module makes to the record.")
            .RequirePermission(ApprovalsPermissions.Decide)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/requests/{id:guid}/reject", RejectAsync)
            .WithName(RejectRouteName)
            .WithSummary("Rejects a request with the reason why, closing it together with the change its module makes to the record.")
            .RequirePermission(ApprovalsPermissions.Decide)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/requests/{id:guid}/withdraw", WithdrawAsync)
            .WithName(WithdrawRouteName)
            .WithSummary("Withdraws a request that still waits, as the person it is for or the person who submitted it, closing it together with the change its module makes to the record.")
            .RequirePermission(ApprovalsPermissions.Withdraw)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static ApprovalAccess AccessOf(ICallerRoles roles, ICallerPermissions permissions) =>
        new(roles.Roles, permissions.Holds(ApprovalsPermissions.ReadAll), permissions.Holds(ApprovalsPermissions.DecideAny));

    private static ApprovalRequestSummaryResponse ToResponse(ApprovalRequestSummary summary) =>
        new(
            summary.Id.Value,
            summary.Kind,
            summary.KindTitle,
            summary.SubjectType,
            summary.SubjectId,
            summary.SubjectName,
            summary.RequestedForId,
            summary.RequestedForName,
            summary.SubmittedById,
            summary.SubmittedByName,
            summary.SubmittedAt,
            summary.State,
            summary.WaitingStepTitle,
            summary.WaitingForKind,
            summary.WaitingForId,
            summary.WaitingForName,
            summary.WaitingForRole,
            summary.DueAt,
            summary.Late,
            summary.ClosedAt);

    private static ApprovalRequestResponse ToResponse(ApprovalRequestDetails details)
    {
        var summary = details.Request;

        return new ApprovalRequestResponse(
            summary.Id.Value,
            summary.Kind,
            summary.KindTitle,
            summary.SubjectType,
            summary.SubjectId,
            summary.SubjectName,
            summary.RequestedForId,
            summary.RequestedForName,
            summary.SubmittedById,
            summary.SubmittedByName,
            summary.SubmittedAt,
            summary.State,
            summary.WaitingStepTitle,
            summary.WaitingForKind,
            summary.WaitingForId,
            summary.WaitingForName,
            summary.WaitingForRole,
            summary.DueAt,
            summary.Late,
            summary.ClosedAt,
            details.Version,
            [.. details.Steps.Select(ToResponse)],
            [.. details.EarlierRequests.Select(ToResponse)],
            details.CanDecide,
            details.DecidesInPlace,
            details.CanWithdraw);
    }

    private static ApprovalStepResponse ToResponse(ApprovalStepDetails step) =>
        new(
            step.Order,
            step.Title,
            step.Rule,
            step.State,
            step.ApproverKind,
            step.ApproverId,
            step.ApproverName,
            step.ApproverRole,
            step.WorkingDays,
            step.StartedAt,
            step.DueAt,
            step.Late,
            step.DecidedById,
            step.DecidedByName,
            step.DecidedAt,
            step.InPlaceOfApprover,
            step.Comment);

    private static EarlierApprovalRequestResponse ToResponse(EarlierApprovalRequest earlier) => new(earlier.Id.Value, earlier.State, earlier.SubmittedAt, earlier.ClosedAt);

    private static async Task<Results<Ok<PagedResponse<ApprovalRequestSummaryResponse>>, ProblemHttpResult>> ListAsync(
        [AsParameters] ListApprovalRequestsRequest request,
        [AsParameters] PagingParameters paging,
        IQueryHandler<ListApprovalRequestsQuery, PagedResult<ApprovalRequestSummary>> handler,
        ICallerRoles roles,
        ICallerPermissions permissions,
        CancellationToken cancellationToken)
    {
        var list = paging.ToListRequest();
        if (list.IsFailure)
        {
            return list.Error!.ToProblem();
        }

        var result = await handler.HandleAsync(new ListApprovalRequestsQuery(request.Selected, list.Value, AccessOf(roles, permissions)), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value.ToResponse(ToResponse)) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ApprovalCountsResponse>, ProblemHttpResult>> CountAsync(
        IQueryHandler<CountApprovalRequestsQuery, ApprovalCounts> handler,
        ICallerRoles roles,
        ICallerPermissions permissions,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CountApprovalRequestsQuery(AccessOf(roles, permissions)), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(new ApprovalCountsResponse(result.Value.WaitingForMe, result.Value.LateForMe, result.Value.MineWaiting))
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ApprovalRequestResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        IQueryHandler<GetApprovalRequestQuery, ApprovalRequestDetails> handler,
        ICallerRoles roles,
        ICallerPermissions permissions,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetApprovalRequestQuery(ApprovalRequestId.From(id), AccessOf(roles, permissions)), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }

    private static Task<Results<Ok<ApprovalRequestResponse>, ProblemHttpResult>> ApproveAsync(
        Guid id,
        ApprovalDecisionRequest request,
        ICommandHandler<DecideApprovalRequestCommand, ApprovalRequestDetails> handler,
        ICallerRoles roles,
        ICallerPermissions permissions,
        CancellationToken cancellationToken) =>
        DecideAsync(id, ApprovalDecision.Approve, request, handler, AccessOf(roles, permissions), cancellationToken);

    private static Task<Results<Ok<ApprovalRequestResponse>, ProblemHttpResult>> ReturnAsync(
        Guid id,
        ApprovalDecisionRequest request,
        ICommandHandler<DecideApprovalRequestCommand, ApprovalRequestDetails> handler,
        ICallerRoles roles,
        ICallerPermissions permissions,
        CancellationToken cancellationToken) =>
        DecideAsync(id, ApprovalDecision.Return, request, handler, AccessOf(roles, permissions), cancellationToken);

    private static Task<Results<Ok<ApprovalRequestResponse>, ProblemHttpResult>> RejectAsync(
        Guid id,
        ApprovalDecisionRequest request,
        ICommandHandler<DecideApprovalRequestCommand, ApprovalRequestDetails> handler,
        ICallerRoles roles,
        ICallerPermissions permissions,
        CancellationToken cancellationToken) =>
        DecideAsync(id, ApprovalDecision.Reject, request, handler, AccessOf(roles, permissions), cancellationToken);

    private static async Task<Results<Ok<ApprovalRequestResponse>, ProblemHttpResult>> DecideAsync(
        Guid id,
        ApprovalDecision decision,
        ApprovalDecisionRequest request,
        ICommandHandler<DecideApprovalRequestCommand, ApprovalRequestDetails> handler,
        ApprovalAccess access,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DecideApprovalRequestCommand(ApprovalRequestId.From(id), decision, request.Comment, request.Version!, access), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ApprovalRequestResponse>, ProblemHttpResult>> WithdrawAsync(
        Guid id,
        ApprovalWithdrawalRequest request,
        ICommandHandler<WithdrawApprovalRequestCommand, ApprovalRequestDetails> handler,
        ICallerRoles roles,
        ICallerPermissions permissions,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new WithdrawApprovalRequestCommand(ApprovalRequestId.From(id), request.Comment, request.Version!, AccessOf(roles, permissions)), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }
}
