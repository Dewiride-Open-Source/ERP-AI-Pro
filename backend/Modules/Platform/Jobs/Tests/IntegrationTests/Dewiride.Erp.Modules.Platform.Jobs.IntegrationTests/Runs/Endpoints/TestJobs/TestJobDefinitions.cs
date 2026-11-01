using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints.TestJobs;

// No host defines RetiredJobName. No test host runs its scheduler, so a recurring test job has no schedule row, and the jobs list
// computes its next occurrence from the configured expression.
internal static class TestJobDefinitions
{
    public const string RetiredJobName = "test.jobs.retired";

    public const string ConfiguredSchedule = " 15  2 * * * ";

    public const string NormalisedSchedule = "15 2 * * *";

    public const string InvalidSchedule = "61 * * * *";

    public static readonly JobDefinition Succeeding = new("test.jobs.succeeding", "Succeeding test job", "Succeeds at once for a test.", JobRetryPolicy.Default, TimeSpan.FromMinutes(1));

    public static readonly JobDefinition Scheduled = new("test.jobs.scheduled", "Scheduled test job", "Runs every night at a quarter past two, India time, for a test.", JobRetryPolicy.Default, TimeSpan.FromMinutes(1));

    public static readonly JobDefinition Misscheduled = new("test.jobs.misscheduled", "Misscheduled test job", "Has a schedule whose minute no hour has, for a test.", JobRetryPolicy.Default, TimeSpan.FromMinutes(1));

    public static WebApplicationFactory<Program> WithTestJobs(this ErpApiFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.Configure<TestJobSchedules>(schedules =>
            {
                schedules.Scheduled = ConfiguredSchedule;
                schedules.Misscheduled = InvalidSchedule;
            });
            services.AddJob<SucceedingJob>(Succeeding);
            services.AddRecurringJob<ScheduledJob, TestJobSchedules>(Scheduled, schedules => schedules.Scheduled);
            services.AddRecurringJob<MisscheduledJob, TestJobSchedules>(Misscheduled, schedules => schedules.Misscheduled);
        }));
}
