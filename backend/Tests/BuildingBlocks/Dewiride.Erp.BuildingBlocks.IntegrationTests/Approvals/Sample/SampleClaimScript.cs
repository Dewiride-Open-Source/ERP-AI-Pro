using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.Approvals.Outcomes;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample;

// What the sample's outcome handler does in one test: it keeps every outcome it is given, misbehaves as the test asks, and, once
// held, waits inside the decision, under the request's lock, until the test releases it.
public sealed class SampleClaimScript
{
    private TaskCompletionSource? _held;

    private TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ConcurrentQueue<ApprovalOutcome> Received { get; } = new();

    public SampleClaimMisbehaviour Misbehaviour { get; set; }

    public Task Reached => _reached.Task;

    public void Hold()
    {
        _reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Release() => _held?.TrySetResult();

    public async Task WaitWhileHeldAsync(CancellationToken cancellationToken)
    {
        _reached.TrySetResult();
        if (_held is { } held)
        {
            await held.Task.WaitAsync(cancellationToken);
        }
    }
}
