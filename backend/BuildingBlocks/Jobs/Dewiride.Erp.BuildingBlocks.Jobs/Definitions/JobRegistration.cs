using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Definitions;

internal sealed record JobRegistration(
    JobDefinition Definition,
    Type JobType,
    Type? PayloadType,
    Func<object, object?, JobContext, CancellationToken, Task<Result>> Invoke,
    Func<IServiceProvider, string?>? Schedule)
{
    public string Name => Definition.Name;

    public bool IsRecurring => Schedule is not null;
}
