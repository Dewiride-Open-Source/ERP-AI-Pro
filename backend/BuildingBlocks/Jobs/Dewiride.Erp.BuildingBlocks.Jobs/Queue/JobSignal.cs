namespace Dewiride.Erp.BuildingBlocks.Jobs.Queue;

// Wakes this process's runner as soon as there is work for it: a run enqueued or re-run, an occurrence enqueued, a slot freed.
// A wake that comes while the runner polls is kept, so the runner polls once more instead of sleeping through it; the runner
// resets the signal before it polls, so the poll that follows sees whatever a wake it consumed announced.
internal sealed class JobSignal
{
    private TaskCompletionSource _wake = NewWake();

    public void Wake() => Volatile.Read(ref _wake).TrySetResult();

    public async Task WaitAsync(TimeSpan timeout, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var wake = Volatile.Read(ref _wake);
        try
        {
            await wake.Task.WaitAsync(timeout, timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
        finally
        {
            if (wake.Task.IsCompleted)
            {
                Interlocked.CompareExchange(ref _wake, NewWake(), wake);
            }
        }
    }

    private static TaskCompletionSource NewWake() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
