using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;

// What the test jobs of a test do, shared by both runners of the test so that every execution is counted in one place. Started
// completes when the first execution begins and Release lets a job that waits for it return.
public sealed class JobScript
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Func<JobContext, CancellationToken, Task<Result>> Run { get; set; } = (_, _) => Task.FromResult(Result.Success());

    public ConcurrentQueue<JobExecution> Executions { get; } = new();

    public ConcurrentQueue<ItemPayload> Payloads { get; } = new();

    public Task Started => _started.Task;

    public bool TokenFired { get; private set; }

    public static JobScript Succeeding() => new();

    public static JobScript Failing(Error error) => new() { Run = (_, _) => Task.FromResult(Result.Fail(error)) };

    public void Release() => _released.TrySetResult();

    public void MarkStarted() => _started.TrySetResult();

    public JobScript WaitingForRelease()
    {
        Run = async (_, _) =>
        {
            await _released.Task;
            return Result.Success();
        };

        return this;
    }

    public JobScript WaitingForItsToken()
    {
        Run = async (_, cancellationToken) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TokenFired = true;
                throw;
            }

            return Result.Success();
        };

        return this;
    }
}
