namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;

// An application lifetime the test starts by hand. StartRequested completes once something asks for ApplicationStarted, as the
// runner's loop does at its gate, so the test knows the loop reached the gate before it checks that nothing was claimed. The
// application never stops on its own: the test stops the service it starts.
public sealed class ManualApplicationLifetime : IHostApplicationLifetime, IDisposable
{
    private readonly TaskCompletionSource _startRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly CancellationTokenSource _started = new();

    public Task StartRequested => _startRequested.Task;

    public CancellationToken ApplicationStarted
    {
        get
        {
            _startRequested.TrySetResult();
            return _started.Token;
        }
    }

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void NotifyStarted() => _started.Cancel();

    public void StopApplication() => throw new NotSupportedException("The test stops the service it started with StopAsync.");

    public void Dispose() => _started.Dispose();
}
