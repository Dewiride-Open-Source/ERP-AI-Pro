using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Testing.EndToEnd.Jobs;

// The end-to-end host's own recurring jobs, which no other host defines: one on a valid schedule and one on a schedule that
// cannot run, so the jobs page shows a job's last run and a schedule's problem beside the account check, whose schedule row no
// spec seeds. The host runs no job loop, so neither job ever runs.
public static class EndToEndJobs
{
    public const string Schedule = "15 2 * * *";

    public const string InvalidSchedule = "61 * * * *";

    public static readonly JobDefinition Scheduled = new(
        "test.jobs.end-to-end-scheduled",
        "Scheduled test job",
        "Runs every night at a quarter past two, India time, for the end-to-end tests.",
        JobRetryPolicy.Default,
        TimeSpan.FromMinutes(1));

    public static readonly JobDefinition Misscheduled = new(
        "test.jobs.end-to-end-misscheduled",
        "Misscheduled test job",
        "Has a schedule whose minute no hour has, for the end-to-end tests.",
        JobRetryPolicy.Default,
        TimeSpan.FromMinutes(1));

    public static IReadOnlyList<string> Names { get; } = [Scheduled.Name, Misscheduled.Name];

    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<EndToEndJobSchedules>(schedules =>
        {
            schedules.Scheduled = Schedule;
            schedules.Misscheduled = InvalidSchedule;
        });
        services.AddRecurringJob<ScheduledTestJob, EndToEndJobSchedules>(Scheduled, schedules => schedules.Scheduled);
        services.AddRecurringJob<MisscheduledTestJob, EndToEndJobSchedules>(Misscheduled, schedules => schedules.Misscheduled);
    }
}
