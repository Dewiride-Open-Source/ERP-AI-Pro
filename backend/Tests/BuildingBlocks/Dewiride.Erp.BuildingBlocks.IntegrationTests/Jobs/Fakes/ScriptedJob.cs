using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Origins;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;

public sealed class ScriptedJob(JobScript script, IActorContext actor, IRequestOrigin origin) : IJob
{
    public Task<Result> RunAsync(JobContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        script.Executions.Enqueue(new JobExecution(context.RunId, context.Attempt, actor.ActorId, origin.CorrelationId));
        script.MarkStarted();

        return script.Run(context, cancellationToken);
    }
}
