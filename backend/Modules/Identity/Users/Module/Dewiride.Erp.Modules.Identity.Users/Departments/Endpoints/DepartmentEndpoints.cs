using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
using Dewiride.Erp.Modules.Identity.Users.Departments.Application;
using Dewiride.Erp.Modules.Identity.Users.Departments.Application.Commands.CreateDepartment;
using Dewiride.Erp.Modules.Identity.Users.Departments.Application.Commands.DeleteDepartment;
using Dewiride.Erp.Modules.Identity.Users.Departments.Application.Commands.UpdateDepartment;
using Dewiride.Erp.Modules.Identity.Users.Departments.Application.Queries.GetDepartment;
using Dewiride.Erp.Modules.Identity.Users.Departments.Application.Queries.ListDepartments;
using Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints.Requests;
using Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints;

// A change answers the department as it was saved, read again, so its version is the one to change next.
internal static class DepartmentEndpoints
{
    public const string GetRouteName = "Identity.Users.GetDepartment";

    public static void Map(RouteGroupBuilder group)
    {
        var departments = group.MapGroup("/departments");

        departments.MapGet("/", ListAsync)
            .WithName("Identity.Users.ListDepartments")
            .WithSummary("Lists the departments, by name unless the caller sorts them; each names the department it sits under.")
            .RequirePermission(UsersPermissions.ReadDepartments);

        departments.MapGet("/{id:guid}", GetAsync)
            .WithName(GetRouteName)
            .WithSummary("Describes one department.")
            .RequirePermission(UsersPermissions.ReadDepartments);

        departments.MapPost("/", CreateAsync)
            .WithName("Identity.Users.CreateDepartment")
            .WithSummary("Creates a department at the top of the tree or under another.")
            .RequirePermission(UsersPermissions.CreateDepartments)
            .RequireIdempotencyKey()
            .ProducesProblem(StatusCodes.Status409Conflict);

        departments.MapPut("/{id:guid}", UpdateAsync)
            .WithName("Identity.Users.UpdateDepartment")
            .WithSummary("Renames a department, or moves it under another department or to the top of the tree.")
            .RequirePermission(UsersPermissions.UpdateDepartments)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        departments.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("Identity.Users.DeleteDepartment")
            .WithSummary("Deletes a department that has no departments within it and that no position names today or from a later date.")
            .RequirePermission(UsersPermissions.DeleteDepartments)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static DepartmentResponse ToResponse(DepartmentDetails details) =>
        new(details.Id, details.Name, details.ParentId, details.Version);

    private static async Task<Results<Ok<PagedResponse<DepartmentResponse>>, ProblemHttpResult>> ListAsync(
        [AsParameters] PagingParameters paging,
        IQueryHandler<ListDepartmentsQuery, PagedResult<DepartmentDetails>> handler,
        CancellationToken cancellationToken)
    {
        var request = paging.ToListRequest();
        if (request.IsFailure)
        {
            return request.Error!.ToProblem();
        }

        var result = await handler.HandleAsync(new ListDepartmentsQuery(request.Value), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value.ToResponse(ToResponse)) : result.Error!.ToProblem();
    }

    private static Task<Results<Ok<DepartmentResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        IQueryHandler<GetDepartmentQuery, DepartmentDetails> handler,
        CancellationToken cancellationToken) =>
        ReadAsync(handler, id, cancellationToken);

    private static async Task<Results<Created<DepartmentResponse>, ProblemHttpResult>> CreateAsync(
        CreateDepartmentRequest request,
        ICommandHandler<CreateDepartmentCommand, Guid> handler,
        IQueryHandler<GetDepartmentQuery, DepartmentDetails> departments,
        LinkGenerator links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var created = await handler.HandleAsync(new CreateDepartmentCommand(request.Name!, request.ParentId), cancellationToken);
        if (created.IsFailure)
        {
            return created.Error!.ToProblem();
        }

        var department = await departments.HandleAsync(new GetDepartmentQuery(created.Value), cancellationToken);
        if (department.IsFailure)
        {
            return department.Error!.ToProblem();
        }

        var location = links.GetPathByName(httpContext, GetRouteName, new { id = created.Value })
            ?? throw new InvalidOperationException($"No endpoint is named '{GetRouteName}'.");

        return TypedResults.Created(location, ToResponse(department.Value));
    }

    private static async Task<Results<Ok<DepartmentResponse>, ProblemHttpResult>> UpdateAsync(
        Guid id,
        UpdateDepartmentRequest request,
        ICommandHandler<UpdateDepartmentCommand, Guid> handler,
        IQueryHandler<GetDepartmentQuery, DepartmentDetails> departments,
        CancellationToken cancellationToken)
    {
        var updated = await handler.HandleAsync(new UpdateDepartmentCommand(id, request.Name!, request.ParentId, request.Version!), cancellationToken);

        return updated.IsFailure ? updated.Error!.ToProblem() : await ReadAsync(departments, updated.Value, cancellationToken);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id,
        ICommandHandler<DeleteDepartmentCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteDepartmentCommand(id), cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<DepartmentResponse>, ProblemHttpResult>> ReadAsync(
        IQueryHandler<GetDepartmentQuery, DepartmentDetails> departments,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await departments.HandleAsync(new GetDepartmentQuery(id), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }
}
