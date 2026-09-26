using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.Attachments.Scanning;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints.Scanning;

internal sealed class FixedVerdictScanner(AttachmentScanVerdict verdict) : IAttachmentScanner
{
    private readonly ConcurrentQueue<byte[]> _scanned = new();

    public AttachmentScanVerdict Verdict { get; } = verdict;

    public IReadOnlyCollection<byte[]> Scanned => _scanned;

    public ValueTask<IAttachmentScan> StartAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IAttachmentScan>(new Scan(this));

    private sealed class Scan(FixedVerdictScanner scanner) : IAttachmentScan
    {
        private readonly MemoryStream _plaintext = new();

        public ValueTask AppendAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken)
        {
            _plaintext.Write(plaintext.Span);

            return ValueTask.CompletedTask;
        }

        public ValueTask<AttachmentScanVerdict> CompleteAsync(CancellationToken cancellationToken)
        {
            scanner._scanned.Enqueue(_plaintext.ToArray());

            return ValueTask.FromResult(scanner.Verdict);
        }

        public ValueTask DisposeAsync() => _plaintext.DisposeAsync();
    }
}
