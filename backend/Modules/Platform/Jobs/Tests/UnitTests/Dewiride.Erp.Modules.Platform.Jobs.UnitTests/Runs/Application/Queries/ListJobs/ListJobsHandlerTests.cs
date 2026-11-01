using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.ListJobs;

namespace Dewiride.Erp.Modules.Platform.Jobs.UnitTests.Runs.Application.Queries.ListJobs;

public sealed class ListJobsHandlerTests
{
    [Fact]
    public async Task HandleAsync_JobsTheReleaseDefines_ReturnsThemAsTheAdministrationListsThem()
    {
        IReadOnlyList<JobDetails> jobs =
        [
            new("identity.users.directory-account-check", "Microsoft account check", "Deactivates the people whose Microsoft account is disabled or deleted.", 6, new JobScheduleDetails("30 0 * * *", JobRunSamples.OccurrenceAt, null, null, null, null)),
            new("platform.files.thumbnails", "Thumbnails", "Draws the thumbnail of an uploaded image.", 5, null),
        ];
        var administration = new RecordingJobAdministration { Jobs = jobs };

        var result = await new ListJobsHandler(administration).HandleAsync(new ListJobsQuery(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(jobs, result.Value);
        Assert.Equal([nameof(IJobAdministration.ListJobsAsync)], administration.Calls);
        Assert.Equal(TestContext.Current.CancellationToken, administration.ReceivedCancellation);
    }
}
