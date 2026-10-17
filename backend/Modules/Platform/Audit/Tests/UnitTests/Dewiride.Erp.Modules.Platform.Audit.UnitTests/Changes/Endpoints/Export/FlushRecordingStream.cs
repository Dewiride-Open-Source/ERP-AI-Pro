namespace Dewiride.Erp.Modules.Platform.Audit.UnitTests.Changes.Endpoints.Export;

// Notes how many lines the file held each time it was flushed, which is when a response sends what it holds.
internal sealed class FlushRecordingStream : MemoryStream
{
    public List<int> LinesAtEachFlush { get; } = [];

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        LinesAtEachFlush.Add(GetBuffer().AsSpan(0, (int)Length).Count("\r\n"u8));

        return base.FlushAsync(cancellationToken);
    }
}
