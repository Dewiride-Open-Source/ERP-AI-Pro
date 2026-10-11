using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
using Dewiride.Erp.Modules.Identity.Users.People.Application;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.AssignPersonRoles;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.ChangePersonStatus;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.DeletePerson;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.RegisterPerson;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.UpdatePerson;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.GetPerson;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.ListPeople;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Requests;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints;

// Naming roles for a record that is being created also needs the permission to assign roles, so holding the permission to
// register people is not enough to hand out roles. A change answers the record as it was saved, read again, so its version
// is the one to edit next.
internal static class PeopleEndpoints
{
    public const string GetRouteName = "Identity.Users.Get";

    public static readonly Error AssigningRolesForbidden = Error.Forbidden("request.forbidden", $"Naming roles for a person needs the permission {UsersPermissions.AssignRoles}.");

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("Identity.Users.List")
            .WithSummary("Lists the people of the ERP, by display name unless the caller sorts them.")
            .RequirePermission(UsersPermissions.Read);

        group.MapGet("/{id:guid}", GetAsync)
            .WithName(GetRouteName)
            .WithSummary("Describes one person.")
            .RequirePermission(UsersPermissions.Read);

        group.MapPost("/", RegisterAsync)
            .WithName("Identity.Users.Register")
            .WithSummary("Registers a person by work email before their first sign-in, which links the record to their Entra account.")
            .RequirePermission(UsersPermissions.Register)
            .RequireIdempotencyKey()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateAsync)
            .WithName("Identity.Users.Update")
            .WithSummary("Changes the employee code, phone number and date of joining of a person.")
            .RequirePermission(UsersPermissions.Update)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/status", ChangeStatusAsync)
            .WithName("Identity.Users.ChangeStatus")
            .WithSummary("Deactivates or reactivates a person; a deactivated person cannot sign in, and their sessions end at their next request.")
            .RequirePermission(UsersPermissions.ChangeStatus)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{id:guid}/roles", AssignRolesAsync)
            .WithName("Identity.Users.AssignRoles")
            .WithSummary("Replaces the roles a person holds in the ERP, Finance, HR and Manager; their next request holds the new ones.")
            .RequirePermission(UsersPermissions.AssignRoles)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("Identity.Users.Delete")
            .WithSummary("Deletes a person's record; their sessions end at their next request.")
            .RequirePermission(UsersPermissions.Delete)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        InvitationEndpoints.Map(group);
    }

    public static async Task<Results<Created<PersonResponse>, ProblemHttpResult>> CreatedAsync(
        Guid id,
        IQueryHandler<GetPersonQuery, PersonDetails> people,
        LinkGenerator links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var person = await people.HandleAsync(new GetPersonQuery(id), cancellationToken);
        if (person.IsFailure)
        {
            return person.Error!.ToProblem();
        }

        var location = links.GetPathByName(httpContext, GetRouteName, new { id })
            ?? throw new InvalidOperationException($"No endpoint is named '{GetRouteName}'.");

        return TypedResults.Created(location, ToResponse(person.Value));
    }

    private static PersonResponse ToResponse(PersonDetails details) =>
        new(
            details.Id,
            details.EntraObjectId,
            details.DisplayName,
            details.WorkEmail,
            details.EmployeeCode,
            details.PhoneNumber,
            details.DateOfJoining,
            details.DepartmentId,
            details.DepartmentName,
            details.DesignationId,
            details.DesignationName,
            details.ManagerId,
            details.ManagerDisplayName,
            details.Status,
            details.Roles,
            details.LastSignedInAt,
            details.CreatedAt,
            details.Version);

    private static async Task<Results<Ok<PagedResponse<PersonResponse>>, ProblemHttpResult>> ListAsync(
        [AsParameters] PagingParameters paging,
        IQueryHandler<ListPeopleQuery, PagedResult<PersonDetails>> handler,
        CancellationToken cancellationToken)
    {
        var request = paging.ToListRequest();
        if (request.IsFailure)
        {
            return request.Error!.ToProblem();
        }

        var result = await handler.HandleAsync(new ListPeopleQuery(request.Value), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value.ToResponse(ToResponse)) : result.Error!.ToProblem();
    }

    private static Task<Results<Ok<PersonResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        IQueryHandler<GetPersonQuery, PersonDetails> handler,
        CancellationToken cancellationToken) =>
        ReadAsync(handler, id, cancellationToken);

    private static async Task<Results<Created<PersonResponse>, ProblemHttpResult>> RegisterAsync(
        RegisterPersonRequest request,
        ICommandHandler<RegisterPersonCommand, Guid> handler,
        IQueryHandler<GetPersonQuery, PersonDetails> people,
        ICallerPermissions permissions,
        LinkGenerator links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var roles = request.Roles ?? [];
        if (roles.Count > 0 && !permissions.Holds(UsersPermissions.AssignRoles))
        {
            return AssigningRolesForbidden.ToProblem();
        }

        var registered = await handler.HandleAsync(
            new RegisterPersonCommand(
                request.DisplayName!,
                request.WorkEmail!,
                request.EmployeeCode,
                request.PhoneNumber,
                request.DateOfJoining,
                roles),
            cancellationToken);

        return registered.IsFailure
            ? registered.Error!.ToProblem()
            : await CreatedAsync(registered.Value, people, links, httpContext, cancellationToken);
    }

    private static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> UpdateAsync(
        Guid id,
        UpdatePersonRequest request,
        ICommandHandler<UpdatePersonCommand, Guid> handler,
        IQueryHandler<GetPersonQuery, PersonDetails> people,
        CancellationToken cancellationToken)
    {
        var updated = await handler.HandleAsync(
            new UpdatePersonCommand(id, request.EmployeeCode, request.PhoneNumber, request.DateOfJoining, request.Version!),
            cancellationToken);

        return updated.IsFailure ? updated.Error!.ToProblem() : await ReadAsync(people, updated.Value, cancellationToken);
    }

    private static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> ChangeStatusAsync(
        Guid id,
        ChangePersonStatusRequest request,
        ICommandHandler<ChangePersonStatusCommand, Guid> handler,
        IQueryHandler<GetPersonQuery, PersonDetails> people,
        CancellationToken cancellationToken)
    {
        var changed = await handler.HandleAsync(new ChangePersonStatusCommand(id, request.Status, request.Version!), cancellationToken);

        return changed.IsFailure ? changed.Error!.ToProblem() : await ReadAsync(people, changed.Value, cancellationToken);
    }

    private static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> AssignRolesAsync(
        Guid id,
        AssignPersonRolesRequest request,
        ICommandHandler<AssignPersonRolesCommand, Guid> handler,
        IQueryHandler<GetPersonQuery, PersonDetails> people,
        CancellationToken cancellationToken)
    {
        var assigned = await handler.HandleAsync(new AssignPersonRolesCommand(id, request.Roles!, request.Version!), cancellationToken);

        return assigned.IsFailure ? assigned.Error!.ToProblem() : await ReadAsync(people, assigned.Value, cancellationToken);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id,
        ICommandHandler<DeletePersonCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeletePersonCommand(id), cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> ReadAsync(
        IQueryHandler<GetPersonQuery, PersonDetails> people,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await people.HandleAsync(new GetPersonQuery(id), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }
}
