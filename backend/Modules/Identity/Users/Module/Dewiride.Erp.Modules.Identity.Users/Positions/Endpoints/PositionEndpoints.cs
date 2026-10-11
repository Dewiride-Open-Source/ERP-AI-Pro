using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application.Commands.RecordPosition;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application.Commands.WithdrawPosition;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.GetPosition;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.ListPositions;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Requests;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints;

internal static class PositionEndpoints
{
    public const string GetRouteName = "Identity.Users.Positions.Get";

    public static void Map(RouteGroupBuilder group)
    {
        var positions = group.MapGroup("/{id:guid}/positions");

        positions.MapGet("/", ListAsync)
            .WithName("Identity.Users.Positions.List")
            .WithSummary("Lists a person's positions, the latest first unless the caller sorts them.")
            .RequirePermission(UsersPermissions.ReadPositions);

        positions.MapGet("/{positionId:guid}", GetAsync)
            .WithName(GetRouteName)
            .WithSummary("Describes one of a person's positions.")
            .RequirePermission(UsersPermissions.ReadPositions);

        positions.MapPost("/", RecordAsync)
            .WithName("Identity.Users.Positions.Record")
            .WithSummary("Records a person's position from a date, in the past or the future, until their next position starts.")
            .RequirePermission(UsersPermissions.RecordPositions)
            .RequireIdempotencyKey()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        positions.MapDelete("/{positionId:guid}", WithdrawAsync)
            .WithName("Identity.Users.Positions.Withdraw")
            .WithSummary("Withdraws one of a person's positions; the one before it then holds until the next starts.")
            .RequirePermission(UsersPermissions.WithdrawPositions)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static PositionResponse ToResponse(PositionDetails details) =>
        new(
            details.Id,
            details.EffectiveFrom,
            details.DepartmentId,
            details.DepartmentName,
            details.DesignationId,
            details.DesignationName,
            details.ManagerId,
            details.ManagerDisplayName);

    private static async Task<Results<Ok<PagedResponse<PositionResponse>>, ProblemHttpResult>> ListAsync(
        Guid id,
        [AsParameters] PagingParameters paging,
        IQueryHandler<ListPositionsQuery, PagedResult<PositionDetails>> handler,
        CancellationToken cancellationToken)
    {
        var request = paging.ToListRequest();
        if (request.IsFailure)
        {
            return request.Error!.ToProblem();
        }

        var result = await handler.HandleAsync(new ListPositionsQuery(id, request.Value), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value.ToResponse(ToResponse)) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<PositionResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        Guid positionId,
        IQueryHandler<GetPositionQuery, PositionDetails> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetPositionQuery(id, positionId), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }

    private static async Task<Results<Created<PositionResponse>, ProblemHttpResult>> RecordAsync(
        Guid id,
        RecordPositionRequest request,
        ICommandHandler<RecordPositionCommand, Guid> handler,
        IQueryHandler<GetPositionQuery, PositionDetails> positions,
        LinkGenerator links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var recorded = await handler.HandleAsync(
            new RecordPositionCommand(id, request.EffectiveFrom!.Value, request.DepartmentId, request.DesignationId, request.ManagerId),
            cancellationToken);
        if (recorded.IsFailure)
        {
            return recorded.Error!.ToProblem();
        }

        var position = await positions.HandleAsync(new GetPositionQuery(id, recorded.Value), cancellationToken);
        if (position.IsFailure)
        {
            return position.Error!.ToProblem();
        }

        var location = links.GetPathByName(httpContext, GetRouteName, new { id, positionId = recorded.Value })
            ?? throw new InvalidOperationException($"No endpoint is named '{GetRouteName}'.");

        return TypedResults.Created(location, ToResponse(position.Value));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> WithdrawAsync(
        Guid id,
        Guid positionId,
        ICommandHandler<WithdrawPositionCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new WithdrawPositionCommand(id, positionId), cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }
}
