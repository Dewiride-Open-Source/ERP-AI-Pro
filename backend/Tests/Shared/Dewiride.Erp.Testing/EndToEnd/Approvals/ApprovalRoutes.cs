using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Testing.Approvals;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Testing.EndToEnd.Approvals;

// No module asks for approval yet, so a spec shows a request by having the requests route submit one of a kind of
// TestApprovalKinds through ApprovalRequestSeeder, as the person signed in to the call: for that person unless the call names
// another, on a record of its own unless it names one, so that a record sent back can be asked for again, and now unless it says
// how many working days ago, routed as on that day, so a spec shows a late request. A kind the administrators switched off answers
// no request. A call nobody is signed in to is refused, as the API refuses every anonymous call that needs a person. Like the other
// test routes, it is the test host's own: anonymous, taking no antiforgery token whether or not the request carries a session, and
// mapped only in the Testing environment.
public static class ApprovalRoutes
{
    public const string RequestsPath = "/api/__test/approvals/requests";

    public static void Map(IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var environment = routes.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsEnvironment(EndToEndHost.EnvironmentName))
        {
            throw new InvalidOperationException(
                $"Anyone signed in can ask for approval here, so the route is mapped only in the {EndToEndHost.EnvironmentName} environment; this host runs in {environment.EnvironmentName}.");
        }

        routes.MapPost(RequestsPath, SubmitAsync).AllowAnonymous().DisableAntiforgery();
    }

    private static async Task<Results<Ok<ApprovalRequestSubmissionResponse>, ValidationProblem, ProblemHttpResult>> SubmitAsync(
        ApprovalRequestSubmission submission,
        IActorContext actor,
        HttpContext context)
    {
        if (!actor.IsAuthenticated)
        {
            return TypedResults.Problem(title: AuthenticationProblems.UnauthenticatedTitle, statusCode: StatusCodes.Status401Unauthorized);
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (TestApprovalKinds.Find(submission.Kind) is null)
        {
            errors["kind"] = ["Name a test kind of approval, such as test.approvals.manager-01."];
        }

        if (submission.RequestedFor is { } requestedFor && (requestedFor == Guid.Empty || requestedFor == ActorIds.System || requestedFor == ActorIds.Anonymous))
        {
            errors["requestedFor"] = ["Name the Microsoft Entra object id of the person the request is for, or leave it out to ask for yourself."];
        }

        if (submission.SubmittedWorkingDaysAgo is < 0 or > ApprovalRequestSeeder.MaxWorkingDaysAgo)
        {
            errors["submittedWorkingDaysAgo"] = [$"Submit the request 0 to {ApprovalRequestSeeder.MaxWorkingDaysAgo} working days ago."];
        }

        if (submission.SubjectId == Guid.Empty)
        {
            errors["subjectId"] = ["Name the id of the record the request approves, or leave it out for a new record."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var seed = new ApprovalRequestSeed(submission.Kind!, submission.RequestedFor ?? actor.ActorId)
        {
            SubmittedWorkingDaysAgo = submission.SubmittedWorkingDaysAgo ?? 0,
            SubjectId = submission.SubjectId ?? Guid.CreateVersion7(),
        };
        var submitted = await ApprovalRequestSeeder.SubmitAsync(context.RequestServices, seed, context.RequestAborted).ConfigureAwait(false);

        return submitted.IsSuccess
            ? TypedResults.Ok(new ApprovalRequestSubmissionResponse(submitted.Value.RequestId?.Value))
            : submitted.Error!.ToProblem();
    }
}
