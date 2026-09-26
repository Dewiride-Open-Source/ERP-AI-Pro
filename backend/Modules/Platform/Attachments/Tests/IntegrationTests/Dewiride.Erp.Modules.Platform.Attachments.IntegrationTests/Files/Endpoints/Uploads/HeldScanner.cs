using Dewiride.Erp.BuildingBlocks.Attachments.Scanning;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints.Uploads;

// Holds every upload at the start of its scan, inside the endpoint, until released, so a test knows an upload is in flight.
internal sealed class HeldScanner : IAttachmentScanner
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public void Release() => _released.TrySetResult();

    public async ValueTask<IAttachmentScan> StartAsync(CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        await _released.Task.WaitAsync(cancellationToken);

        return new CleanScan();
    }

    private sealed class CleanScan : IAttachmentScan
    {
        public ValueTask AppendAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask<AttachmentScanVerdict> CompleteAsync(CancellationToken cancellationToken) => ValueTask.FromResult(AttachmentScanVerdict.Clean);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
