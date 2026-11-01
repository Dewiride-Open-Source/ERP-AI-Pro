using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints.TestJobs;

public sealed class SucceedingJob : IJob
{
    public Task<Result> RunAsync(JobContext context, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
}
