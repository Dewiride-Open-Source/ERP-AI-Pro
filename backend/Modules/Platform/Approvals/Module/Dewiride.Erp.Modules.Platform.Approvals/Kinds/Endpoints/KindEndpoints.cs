using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Approvals.Administration;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.Modules.Platform.Approvals.Contracts;
using Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Commands.ChangeApprovalKindSetting;
using Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Commands.SetUpApprovalKindSetting;
using Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Queries.ListApprovalKinds;
using Dewiride.Erp.Modules.Platform.Approvals.Kinds.Endpoints.Requests;
using Dewiride.Erp.Modules.Platform.Approvals.Kinds.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Endpoints;

// Everyone who reads requests reads the kinds, whose titles the lists filter by. A setting has no page of its own, so a set-up
// answers the address it is changed at; a change names the version it was made from and takes no idempotency key: repeated, it finds
// the setting at a newer version and is refused.
internal static class KindEndpoints
{
    public const string ListRouteName = "Platform.Approvals.ListApprovalKinds";

    public const string SetUpSettingRouteName = "Platform.Approvals.SetUpApprovalKindSetting";

    public const string ChangeSettingRouteName = "Platform.Approvals.ChangeApprovalKindSetting";

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/kinds", ListAsync)
            .WithName(ListRouteName)
            .WithSummary("Lists the kinds of request the ERP asks approval for, by title, each with its steps, the working days it declares and the administrators' setting once one is set up.")
            .RequirePermission(ApprovalsPermissions.Read);

        group.MapPost("/kind-settings", SetUpAsync)
            .WithName(SetUpSettingRouteName)
            .WithSummary("Sets up whether a kind of request needs approval and how many working days each of its steps has, for the requests submitted from then on.")
            .RequirePermission(ApprovalsPermissions.ChangeKinds)
            .RequireIdempotencyKey()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/kind-settings/{id:guid}", ChangeAsync)
            .WithName(ChangeSettingRouteName)
            .WithSummary("Changes whether a kind of request needs approval and how many working days each of its steps has, for the requests submitted from then on; a request already waiting keeps its route and its due times.")
            .RequirePermission(ApprovalsPermissions.ChangeKinds)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static ApprovalKindResponse ToResponse(ApprovalKindDetails details) =>
        new(
            details.Kind.Key,
            details.Kind.Title,
            details.Kind.Description,
            [.. details.Kind.Steps.Select((step, index) => new ApprovalKindStepResponse(index + 1, step.Title, step.Rule, step.Role))],
            details.Kind.WorkingDaysToDecide,
            details.Setting is { } setting ? new ApprovalKindSettingResponse(setting.Id.Value, setting.Required, setting.WorkingDaysToDecide, setting.Version) : null);

    private static async Task<Results<Ok<IReadOnlyList<ApprovalKindResponse>>, ProblemHttpResult>> ListAsync(
        IQueryHandler<ListApprovalKindsQuery, IReadOnlyList<ApprovalKindDetails>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListApprovalKindsQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        IReadOnlyList<ApprovalKindResponse> kinds = [.. result.Value.Select(ToResponse)];

        return TypedResults.Ok(kinds);
    }

    private static async Task<Results<Created<ApprovalKindResponse>, ProblemHttpResult>> SetUpAsync(
        SetUpApprovalKindSettingRequest request,
        ICommandHandler<SetUpApprovalKindSettingCommand, ApprovalKindDetails> handler,
        LinkGenerator links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new SetUpApprovalKindSettingCommand(request.Kind!, request.Required!.Value, request.WorkingDaysToDecide!.Value), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        var location = links.GetPathByName(httpContext, ChangeSettingRouteName, new { id = result.Value.Setting!.Id.Value })
            ?? throw new InvalidOperationException($"No endpoint is named '{ChangeSettingRouteName}'.");

        return TypedResults.Created(location, ToResponse(result.Value));
    }

    private static async Task<Results<Ok<ApprovalKindResponse>, ProblemHttpResult>> ChangeAsync(
        Guid id,
        ChangeApprovalKindSettingRequest request,
        ICommandHandler<ChangeApprovalKindSettingCommand, ApprovalKindDetails> handler,
        CancellationToken cancellationToken)
    {
        var command = new ChangeApprovalKindSettingCommand(ApprovalKindSettingId.From(id), request.Required!.Value, request.WorkingDaysToDecide!.Value, request.Version!);
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }
}
