using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.Testing.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Testing.EndToEnd.Jobs;

// The end-to-end host runs no job runner, so no run ever executes there, and a spec shows a run in each state by seeding it here
// with the events the engine would have written; the run belongs to whatever job the seed names, defined by the host or not.
// Every field the seed leaves out keeps the value JobRunSeed gives it, and a field it sends is refused rather than adjusted when
// the engine could not have stored it. Like the other test routes, the route is the test host's own: anonymous, taking no
// antiforgery token whether or not the request carries a session, and mapped only in the Testing environment.
public static class JobRuns
{
    public const string Path = "/api/__test/jobs/runs";

    public static void Map(IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var environment = routes.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsEnvironment(EndToEndHost.EnvironmentName))
        {
            throw new InvalidOperationException(
                $"Anyone can seed job runs, so the route is mapped only in the {EndToEndHost.EnvironmentName} environment; this host runs in {environment.EnvironmentName}.");
        }

        routes.MapPost(Path, SeedAsync).AllowAnonymous().DisableAntiforgery();
    }

    private static async Task<Results<Ok<JobRunSeedResponse>, ValidationProblem>> SeedAsync(JobRunSeed seed, HttpContext context)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!JobNames.IsWellFormed(seed.JobName))
        {
            errors["jobName"] = [$"Name the job: three lower-case segments of at most {JobNames.MaxLength} characters, such as identity.users.directory-account-check."];
        }

        if (!JobKeys.IsWellFormed(seed.IdempotencyKey))
        {
            errors["idempotencyKey"] = [$"Give a key of 1 to {JobKeys.MaxLength} letters, digits and the characters . _ : + -."];
        }

        if (!Enum.IsDefined(seed.Status))
        {
            errors["status"] = ["Say whether the run is pending, running, failed, succeeded, deadLettered or cancelled."];
        }

        if (seed.Attempts is < 1 or > JobRetryPolicy.AttemptsCeiling)
        {
            errors["attempts"] = [$"A seeded run has started 1 to {JobRetryPolicy.AttemptsCeiling} attempts."];
        }

        if (seed.ErrorCode is not { Length: > 0 and <= JobRun.ErrorCodeMaxLength } code || !code.All(character => character is > ' ' and <= '~'))
        {
            errors["errorCode"] = [$"An error code is 1 to {JobRun.ErrorCodeMaxLength} printable ASCII characters without spaces."];
        }

        if (string.IsNullOrWhiteSpace(seed.ErrorMessage) || seed.ErrorMessage.Length > JobRun.ErrorMessageMaxLength)
        {
            errors["errorMessage"] = [$"An error message is 1 to {JobRun.ErrorMessageMaxLength} characters."];
        }

        if (seed.StopRequested && seed.Status != JobRunStatus.Running)
        {
            errors["stopRequested"] = ["Only a running run can have been asked to stop."];
        }

        if (seed.RequestedBy == Guid.Empty)
        {
            errors["requestedBy"] = ["Name the Entra object id of whoever enqueued the run, or leave it out for the ERP itself."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var id = await JobRunSeeder.SeedAsync(context.RequestServices, seed, context.RequestAborted).ConfigureAwait(false);

        return TypedResults.Ok(new JobRunSeedResponse(id.Value));
    }
}
