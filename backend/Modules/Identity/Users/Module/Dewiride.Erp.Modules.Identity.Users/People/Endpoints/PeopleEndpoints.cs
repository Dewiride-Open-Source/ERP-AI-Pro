using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.Modules.Identity.Users.People.Application;
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

// Until the ERP has roles and permissions of its own, the administrator app role of the sign-in registration is what lets a
// person manage people. A change answers the record as it was saved, read again, so its version is the one to edit next.
internal static class PeopleEndpoints
{
    public const string GetRouteName = "Identity.Users.Get";

    public static void Map(RouteGroupBuilder group)
    {
        var people = group.MapGroup(string.Empty)
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Administrator))
            .ProducesProblem(StatusCodes.Status403Forbidden);

        people.MapGet("/", ListAsync)
            .WithName("Identity.Users.List")
            .WithSummary("Lists the people of the ERP, by display name unless the caller sorts them.");

        people.MapGet("/{id:guid}", GetAsync)
            .WithName(GetRouteName)
            .WithSummary("Describes one person.");

        people.MapPost("/", RegisterAsync)
            .WithName("Identity.Users.Register")
            .WithSummary("Registers a person before their first sign-in, which links the record to their Entra account.")
            .RequireIdempotencyKey();

        people.MapPut("/{id:guid}", UpdateAsync)
            .WithName("Identity.Users.Update")
            .WithSummary("Changes the employee code, phone number, designation and date of joining of a person.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        people.MapPut("/{id:guid}/status", ChangeStatusAsync)
            .WithName("Identity.Users.ChangeStatus")
            .WithSummary("Deactivates or reactivates a person; a deactivated person cannot sign in, and their sessions end at their next request.")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        people.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("Identity.Users.Delete")
            .WithSummary("Deletes a person's record; their sessions end at their next request.")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static PersonResponse ToResponse(PersonDetails details) =>
        new(
            details.Id,
            details.EntraObjectId,
            details.DisplayName,
            details.WorkEmail,
            details.EmployeeCode,
            details.PhoneNumber,
            details.Designation,
            details.DateOfJoining,
            details.Status,
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
        LinkGenerator links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var registered = await handler.HandleAsync(
            new RegisterPersonCommand(
                request.EntraObjectId,
                request.DisplayName!,
                request.WorkEmail!,
                request.EmployeeCode,
                request.PhoneNumber,
                request.Designation,
                request.DateOfJoining),
            cancellationToken);
        if (registered.IsFailure)
        {
            return registered.Error!.ToProblem();
        }

        var person = await people.HandleAsync(new GetPersonQuery(registered.Value), cancellationToken);
        if (person.IsFailure)
        {
            return person.Error!.ToProblem();
        }

        var location = links.GetPathByName(httpContext, GetRouteName, new { id = registered.Value })
            ?? throw new InvalidOperationException($"No endpoint is named '{GetRouteName}'.");

        return TypedResults.Created(location, ToResponse(person.Value));
    }

    private static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> UpdateAsync(
        Guid id,
        UpdatePersonRequest request,
        ICommandHandler<UpdatePersonCommand, Guid> handler,
        IQueryHandler<GetPersonQuery, PersonDetails> people,
        CancellationToken cancellationToken)
    {
        var updated = await handler.HandleAsync(
            new UpdatePersonCommand(id, request.EmployeeCode, request.PhoneNumber, request.Designation, request.DateOfJoining, request.Version!),
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
