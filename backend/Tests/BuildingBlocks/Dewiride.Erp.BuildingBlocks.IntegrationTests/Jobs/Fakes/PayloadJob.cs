using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;

public sealed class PayloadJob(JobScript script, IActorContext actor) : IJob<ItemPayload>
{
    public Task<Result> RunAsync(ItemPayload payload, JobContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        script.Executions.Enqueue(new JobExecution(context.RunId, context.Attempt, actor.ActorId, null));
        script.Payloads.Enqueue(payload);
        script.MarkStarted();

        return script.Run(context, cancellationToken);
    }
}
