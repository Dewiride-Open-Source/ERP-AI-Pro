using System.Globalization;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Export;

// RFC 4180 lines in UTF-8 with a byte order mark, by which Excel recognises UTF-8: one line per changed value and one for a
// record without any, so a spreadsheet filter on a field finds every change of it. The file is written while the history is
// read, so it never sits whole in memory, and it is flushed after every page of records, so the browser receives it as it grows.
internal static class ChangeCsvWriter
{
    public const string MediaType = "text/csv";

    public const string ContentType = MediaType + "; charset=utf-8";

    public const int RecordsPerFlush = 500;

    private const string LineEnd = "\r\n";

    private static readonly UTF8Encoding Utf8WithByteOrderMark = new(encoderShouldEmitUTF8Identifier: true);

    private static readonly string[] Header =
    [
        "Time (IST)",
        "Time (UTC)",
        "Action",
        "Subject type",
        "Subject id",
        "Subject",
        "Record type",
        "Record id",
        "Changed by",
        "Changed by id",
        "Client address",
        "Correlation id",
        "Field",
        "Before",
        "Before name",
        "After",
        "After name",
    ];

    public static string FileNameOf(ChangeExport export)
    {
        ArgumentNullException.ThrowIfNull(export);

        return string.Create(CultureInfo.InvariantCulture, $"change-history-{IndianStandardTime.ToIst(export.Until):yyyyMMdd-HHmmss}.csv");
    }

    public static async Task WriteAsync(Stream body, ChangeExport export, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(export);

        var writer = new StreamWriter(body, Utf8WithByteOrderMark, leaveOpen: true);
        await using (writer.ConfigureAwait(false))
        {
            await WriteLineAsync(writer, Header, cancellationToken).ConfigureAwait(false);
            var written = 0;
            await foreach (var record in export.Read(cancellationToken).ConfigureAwait(false))
            {
                foreach (var line in LinesOf(record))
                {
                    await WriteLineAsync(writer, line, cancellationToken).ConfigureAwait(false);
                }

                if (++written % RecordsPerFlush == 0)
                {
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<string?[]> LinesOf(ChangeRecordDetails record)
    {
        if (record.Changes.Count == 0)
        {
            yield return Line(record, null);
            yield break;
        }

        foreach (var value in record.Changes)
        {
            yield return Line(record, value);
        }
    }

    private static string?[] Line(ChangeRecordDetails record, ChangedValueDetails? value) =>
    [
        IndianStandardTime.ToIst(record.OccurredAt).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
        record.OccurredAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        record.Operation.ToString(),
        record.SubjectType,
        record.SubjectId.ToString("D"),
        record.SubjectName,
        record.EntityType,
        record.EntityId.ToString("D"),
        ActorOf(record),
        record.ActorId.ToString("D"),
        record.ClientAddress,
        record.CorrelationId,
        value?.Property,
        value?.Before,
        value?.BeforeName,
        value?.After,
        value?.AfterName,
    ];

    private static string? ActorOf(ChangeRecordDetails record) =>
        record.ActorKind switch
        {
            ChangeActorKind.System => "The ERP",
            ChangeActorKind.Anonymous => "Someone not signed in",
            _ => record.ActorName,
        };

    private static Task WriteLineAsync(StreamWriter writer, IEnumerable<string?> cells, CancellationToken cancellationToken) =>
        writer.WriteAsync((string.Join(',', cells.Select(CsvCells.Of)) + LineEnd).AsMemory(), cancellationToken);
}
