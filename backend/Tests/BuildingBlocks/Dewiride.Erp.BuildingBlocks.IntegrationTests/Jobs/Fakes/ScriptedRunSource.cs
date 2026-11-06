using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;

// A run source whose test chooses, before the runner starts, how many polls it fails and the key under which it then enqueues one
// run of the scripted job; with no key it enqueues nothing.
public sealed class ScriptedRunSource(IServiceScopeFactory scopes) : IJobRunSource
{
    private int _failuresLeft;

    public string? Key { get; set; }

    public void FailPolls(int count) => _failuresLeft = count;

    public async Task EnqueueDueAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Decrement(ref _failuresLeft) >= 0)
        {
            throw new InvalidOperationException("The scripted run source failed.");
        }

        if (Key is { } key)
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueAsync<ScriptedJob>(key, cancellationToken);
        }
    }
}
