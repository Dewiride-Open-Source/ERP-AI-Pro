using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.Modules.Identity.Users.People.Application;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.InvitePerson;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.FindRegisteredPeople;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.GetPerson;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Requests;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints;

// The directory is read on behalf of the signed-in administrator, so only a request can read it: these endpoints read it and
// hand the people it returned to the handlers, which never read it themselves, so the hosts that compose the module without
// a signed-in person need none of the sign-in services. An invitation takes the display name and the sign-in name from the
// directory, never from the caller.
internal static class InvitationEndpoints
{
    public static void Map(RouteGroupBuilder people)
    {
        people.MapGet("/directory", SearchDirectoryAsync)
            .WithName("Identity.Users.SearchDirectory")
            .WithSummary("Searches the company directory in Microsoft Entra on the administrator's behalf and names the people the ERP already holds.")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        people.MapPost("/invitations", InviteAsync)
            .WithName("Identity.Users.Invite")
            .WithSummary("Registers a person of the company directory by their Entra object id, with the name and sign-in name the directory holds, so their first sign-in finds the record.")
            .RequireIdempotencyKey()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<Results<Ok<DirectorySearchResponse>, ProblemHttpResult>> SearchDirectoryAsync(
        [AsParameters] SearchDirectoryRequest request,
        IPeopleDirectory directory,
        IQueryHandler<FindRegisteredPeopleQuery, IReadOnlyDictionary<Guid, Guid>> registered,
        CancellationToken cancellationToken)
    {
        var found = await directory.SearchAsync(request.Search!, cancellationToken);
        if (found.IsFailure)
        {
            return found.Error!.ToProblem();
        }

        var people = found.Value.People;
        var records = await registered.HandleAsync(new FindRegisteredPeopleQuery(people), cancellationToken);
        if (records.IsFailure)
        {
            return records.Error!.ToProblem();
        }

        return TypedResults.Ok(new DirectorySearchResponse(
            [.. people.Select(person => new DirectoryPersonResponse(
                person.ObjectId,
                person.DisplayName,
                person.SignInName,
                person.UserPrincipalName,
                person.Mail,
                records.Value.TryGetValue(person.ObjectId, out var personId) ? personId : null))],
            found.Value.HasMore));
    }

    private static async Task<Results<Created<PersonResponse>, ProblemHttpResult>> InviteAsync(
        InvitePersonRequest request,
        IPeopleDirectory directory,
        ICommandHandler<InvitePersonCommand, Guid> handler,
        IQueryHandler<GetPersonQuery, PersonDetails> people,
        LinkGenerator links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var found = await directory.FindAsync(request.EntraObjectId!.Value, cancellationToken);
        if (found.IsFailure)
        {
            return found.Error!.ToProblem();
        }

        var invited = await handler.HandleAsync(
            new InvitePersonCommand(found.Value, request.EmployeeCode, request.PhoneNumber, request.Designation, request.DateOfJoining),
            cancellationToken);

        return invited.IsFailure
            ? invited.Error!.ToProblem()
            : await PeopleEndpoints.CreatedAsync(invited.Value, people, links, httpContext, cancellationToken);
    }
}
