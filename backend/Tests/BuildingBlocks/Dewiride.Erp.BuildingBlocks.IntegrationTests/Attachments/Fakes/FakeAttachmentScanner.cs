using System.Buffers;
using Dewiride.Erp.BuildingBlocks.Attachments.Scanning;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Fakes;

internal sealed class FakeAttachmentScanner(AttachmentScanVerdict verdict) : IAttachmentScanner
{
    private readonly ArrayBufferWriter<byte> _received = new();

    public AttachmentScanVerdict Verdict { get; } = verdict;

    public byte[] Received => _received.WrittenSpan.ToArray();

    public int Starts { get; private set; }

    public int Completions { get; private set; }

    public int Disposals { get; private set; }

    public ValueTask<IAttachmentScan> StartAsync(CancellationToken cancellationToken)
    {
        Starts++;

        return ValueTask.FromResult<IAttachmentScan>(new Scan(this));
    }

    // The uploader reuses its read buffer, so the plaintext is copied the moment it arrives.
    private sealed class Scan(FakeAttachmentScanner scanner) : IAttachmentScan
    {
        public ValueTask AppendAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken)
        {
            scanner._received.Write(plaintext.Span);

            return ValueTask.CompletedTask;
        }

        public ValueTask<AttachmentScanVerdict> CompleteAsync(CancellationToken cancellationToken)
        {
            scanner.Completions++;

            return ValueTask.FromResult(scanner.Verdict);
        }

        public ValueTask DisposeAsync()
        {
            scanner.Disposals++;

            return ValueTask.CompletedTask;
        }
    }
}
