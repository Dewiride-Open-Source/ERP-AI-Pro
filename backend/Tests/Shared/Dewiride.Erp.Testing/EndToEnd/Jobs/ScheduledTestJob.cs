using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Testing.EndToEnd.Jobs;

public sealed class ScheduledTestJob : IJob
{
    public Task<Result> RunAsync(JobContext context, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
}
