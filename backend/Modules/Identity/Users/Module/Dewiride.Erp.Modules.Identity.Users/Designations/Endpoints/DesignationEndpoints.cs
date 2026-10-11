using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.Designations.Application;
using Dewiride.Erp.Modules.Identity.Users.Designations.Application.Commands.CreateDesignation;
using Dewiride.Erp.Modules.Identity.Users.Designations.Application.Commands.DeleteDesignation;
using Dewiride.Erp.Modules.Identity.Users.Designations.Application.Commands.UpdateDesignation;
using Dewiride.Erp.Modules.Identity.Users.Designations.Application.Queries.GetDesignation;
using Dewiride.Erp.Modules.Identity.Users.Designations.Application.Queries.ListDesignations;
using Dewiride.Erp.Modules.Identity.Users.Designations.Endpoints.Requests;
using Dewiride.Erp.Modules.Identity.Users.Designations.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Endpoints;

// A change answers the designation as it was saved, read again, so its version is the one to change next.
internal static class DesignationEndpoints
{
    public const string GetRouteName = "Identity.Users.Designations.Get";

    public static void Map(RouteGroupBuilder group)
    {
        var designations = group.MapGroup("/designations");

        designations.MapGet("/", ListAsync)
            .WithName("Identity.Users.Designations.List")
            .WithSummary("Lists the designations, by name unless the caller sorts them.")
            .RequirePermission(UsersPermissions.ReadDesignations);

        designations.MapGet("/{id:guid}", GetAsync)
            .WithName(GetRouteName)
            .WithSummary("Describes one designation.")
            .RequirePermission(UsersPermissions.ReadDesignations);

        designations.MapPost("/", CreateAsync)
            .WithName("Identity.Users.Designations.Create")
            .WithSummary("Adds a designation to the list positions choose from.")
            .RequirePermission(UsersPermissions.CreateDesignations)
            .RequireIdempotencyKey()
            .ProducesProblem(StatusCodes.Status409Conflict);

        designations.MapPut("/{id:guid}", UpdateAsync)
            .WithName("Identity.Users.Designations.Update")
            .WithSummary("Renames a designation.")
            .RequirePermission(UsersPermissions.UpdateDesignations)
            .ProducesProblem(StatusCodes.Status409Conflict);

        designations.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("Identity.Users.Designations.Delete")
            .WithSummary("Deletes a designation that no position names today or from a later date.")
            .RequirePermission(UsersPermissions.DeleteDesignations)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static DesignationResponse ToResponse(DesignationDetails details) =>
        new(details.Id, details.Name, details.Version);

    private static async Task<Results<Ok<PagedResponse<DesignationResponse>>, ProblemHttpResult>> ListAsync(
        [AsParameters] PagingParameters paging,
        IQueryHandler<ListDesignationsQuery, PagedResult<DesignationDetails>> handler,
        CancellationToken cancellationToken)
    {
        var request = paging.ToListRequest();
        if (request.IsFailure)
        {
            return request.Error!.ToProblem();
        }

        var result = await handler.HandleAsync(new ListDesignationsQuery(request.Value), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value.ToResponse(ToResponse)) : result.Error!.ToProblem();
    }

    private static Task<Results<Ok<DesignationResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        IQueryHandler<GetDesignationQuery, DesignationDetails> handler,
        CancellationToken cancellationToken) =>
        ReadAsync(handler, id, cancellationToken);

    private static async Task<Results<Created<DesignationResponse>, ProblemHttpResult>> CreateAsync(
        CreateDesignationRequest request,
        ICommandHandler<CreateDesignationCommand, Guid> handler,
        IQueryHandler<GetDesignationQuery, DesignationDetails> designations,
        LinkGenerator links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var created = await handler.HandleAsync(new CreateDesignationCommand(request.Name!), cancellationToken);
        if (created.IsFailure)
        {
            return created.Error!.ToProblem();
        }

        var designation = await designations.HandleAsync(new GetDesignationQuery(created.Value), cancellationToken);
        if (designation.IsFailure)
        {
            return designation.Error!.ToProblem();
        }

        var location = links.GetPathByName(httpContext, GetRouteName, new { id = created.Value })
            ?? throw new InvalidOperationException($"No endpoint is named '{GetRouteName}'.");

        return TypedResults.Created(location, ToResponse(designation.Value));
    }

    private static async Task<Results<Ok<DesignationResponse>, ProblemHttpResult>> UpdateAsync(
        Guid id,
        UpdateDesignationRequest request,
        ICommandHandler<UpdateDesignationCommand, Guid> handler,
        IQueryHandler<GetDesignationQuery, DesignationDetails> designations,
        CancellationToken cancellationToken)
    {
        var updated = await handler.HandleAsync(new UpdateDesignationCommand(id, request.Name!, request.Version!), cancellationToken);

        return updated.IsFailure ? updated.Error!.ToProblem() : await ReadAsync(designations, updated.Value, cancellationToken);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id,
        ICommandHandler<DeleteDesignationCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteDesignationCommand(id), cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<DesignationResponse>, ProblemHttpResult>> ReadAsync(
        IQueryHandler<GetDesignationQuery, DesignationDetails> designations,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await designations.HandleAsync(new GetDesignationQuery(id), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }
}
