using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Testing.EndToEnd.Jobs;

// The end-to-end host runs no scheduler, so a recurring job has no schedule row there until a spec seeds one to show the job's
// last run, and only the host's two test jobs take one: every browser project shares the host, whose jobs page must show the
// account check as not run yet. A job has one row, so the first seed of a job's last run stays and every seed answers the row
// as it is stored, which lets the projects seed the same job at once; a spec reads the last run from the answer, not from its
// seed. The row keeps the next occurrence of the job's configured schedule, or the configured text when it does not parse,
// which no page reads while the configuration differs from it. Like the other test routes, the route is the test host's own:
// anonymous, taking no antiforgery token, and mapped only in the Testing environment.
public static class JobSchedules
{
    public const string Path = "/api/__test/jobs/schedules";

    public static void Map(IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var environment = routes.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsEnvironment(EndToEndHost.EnvironmentName))
        {
            throw new InvalidOperationException(
                $"Anyone can seed the schedule of a job, so the route is mapped only in the {EndToEndHost.EnvironmentName} environment; this host runs in {environment.EnvironmentName}.");
        }

        routes.MapPost(Path, SeedAsync).AllowAnonymous().DisableAntiforgery();
    }

    private static async Task<Results<Ok<JobScheduleSeedResponse>, ValidationProblem>> SeedAsync(JobScheduleSeed seed, HttpContext context)
    {
        var services = context.RequestServices;
        var jobs = services.GetRequiredService<JobsDbContext>();
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var registration = seed.JobName is { } name && EndToEndJobs.Names.Contains(name) ? services.GetRequiredService<JobCatalog>().Find(name) : null;
        if (registration is not { IsRecurring: true })
        {
            errors["jobName"] = [$"Name one of the host's test jobs, {EndToEndJobs.Scheduled.Name} or {EndToEndJobs.Misscheduled.Name}: every browser project shares the host, so the account check's schedule is never seeded."];
        }

        if (seed.LastRunId is not { } lastRunId || lastRunId == Guid.Empty)
        {
            errors["lastRunId"] = ["Name the job's last run: the id of a run seeded for the job, or an id no run has."];
        }
        else if (await JobOfRunAsync(jobs, JobRunId.From(lastRunId), context.RequestAborted).ConfigureAwait(false) is { } owner
            && !string.Equals(owner, seed.JobName, StringComparison.Ordinal))
        {
            errors["lastRunId"] = [$"The run belongs to {owner}; name a run of the job, or an id no run has."];
        }

        if (seed.LastOccurrenceAt is null)
        {
            errors["lastOccurrenceAt"] = ["Give the time of the occurrence whose run is the job's last run."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var stored = await SeedRowAsync(services, jobs, registration!, JobRunId.From(seed.LastRunId!.Value), seed.LastOccurrenceAt!.Value, context.RequestAborted).ConfigureAwait(false);

        return TypedResults.Ok(new JobScheduleSeedResponse(stored.JobName, stored.LastRunId!.Value.Value, stored.LastOccurrenceAt!.Value));
    }

    private static async Task<JobSchedule> SeedRowAsync(
        IServiceProvider services,
        JobsDbContext jobs,
        JobRegistration registration,
        JobRunId lastRunId,
        DateTimeOffset lastOccurrenceAt,
        CancellationToken cancellationToken)
    {
        var name = registration.Name;
        if (await StoredAsync(jobs, name, cancellationToken).ConfigureAwait(false) is not { LastRunId: not null })
        {
            var configured = registration.Schedule!(services) ?? string.Empty;
            var parsed = CronSchedule.Parse(configured);
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
            var row = parsed.IsSuccess ? JobSchedule.Start(name, parsed.Value.Expression, parsed.Value.NextAfter(now)) : JobSchedule.Start(name, configured.Trim(), now);
            await jobs.Database.CreateExecutionStrategy().ExecuteAsync(
                async token =>
                {
                    jobs.ChangeTracker.Clear();
                    await using var transaction = await jobs.Database.BeginTransactionAsync(token).ConfigureAwait(false);
                    await JobScheduleTransitions.TryStartAsync(jobs, row, token).ConfigureAwait(false);
                    await jobs.Schedules
                        .Where(s => s.JobName == name && s.LastRunId == null)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.LastOccurrenceAt, (DateTimeOffset?)lastOccurrenceAt).SetProperty(s => s.LastRunId, (JobRunId?)lastRunId), token)
                        .ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }

        return (await StoredAsync(jobs, name, cancellationToken).ConfigureAwait(false))!;
    }

    private static Task<string?> JobOfRunAsync(JobsDbContext jobs, JobRunId runId, CancellationToken cancellationToken) =>
        jobs.Runs.AsNoTracking().Where(r => r.Id == runId).Select(r => (string?)r.JobName).SingleOrDefaultAsync(cancellationToken);

    private static Task<JobSchedule?> StoredAsync(JobsDbContext jobs, string name, CancellationToken cancellationToken) =>
        jobs.Schedules.AsNoTracking().SingleOrDefaultAsync(s => s.JobName == name, cancellationToken);
}
