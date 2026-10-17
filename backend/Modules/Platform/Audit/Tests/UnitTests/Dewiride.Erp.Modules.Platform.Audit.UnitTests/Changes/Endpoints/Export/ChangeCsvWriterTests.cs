using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Export;

namespace Dewiride.Erp.Modules.Platform.Audit.UnitTests.Changes.Endpoints.Export;

public sealed class ChangeCsvWriterTests
{
    private const string HeaderLine =
        "\"Time (IST)\",\"Time (UTC)\",\"Action\",\"Subject type\",\"Subject id\",\"Subject\",\"Record type\",\"Record id\",\"Changed by\",\"Changed by id\","
        + "\"Client address\",\"Correlation id\",\"Field\",\"Before\",\"Before name\",\"After\",\"After name\"";

    private const string ClientAddress = "198.51.100.23";

    private const string CorrelationId = "0af7651916cd43dd8448eb211c80319c";

    private static readonly Guid PersonId = Guid.Parse("0199f1e2-7c4a-7b1d-9e30-5a6b7c8d9e0f");

    private static readonly Guid PositionId = Guid.Parse("0199f1e2-8d5b-7c2e-8f41-6b7c8d9e0f1a");

    private static readonly Guid DepartmentId = Guid.Parse("0199f1e2-9e6c-7d3f-a052-7c8d9e0f1a2b");

    private static readonly Guid AdministratorObjectId = Guid.Parse("8b6e1f47-2c9a-4d3b-b5e0-7a19c4d2e865");

    private static readonly DateTimeOffset LateEveningUtc = new(2026, 10, 16, 18, 45, 30, 123, TimeSpan.Zero);

    [Fact]
    public async Task WriteAsync_NoRecords_WritesTheByteOrderMarkAndTheHeaderLine()
    {
        var bytes = await WriteAsync();

        Assert.Equal<byte>([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal(HeaderLine + "\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public async Task WriteAsync_RecordWithValues_WritesOneLinePerValueWithItsTimeInIndianStandardTimeAndUtc()
    {
        var record = Record(
            ChangeOperation.Created,
            ChangeActorKind.Person,
            "Vikram Iyer",
            new ChangedValueDetails("departmentId", ChangeValueKind.Reference, null, DepartmentId.ToString("D"), "identity.users.departments", null, "Finance"),
            new ChangedValueDetails("effectiveFrom", ChangeValueKind.Date, null, "2026-04-01", null, null, null));

        var lines = Lines(await WriteAsync(record));

        string[] shared =
        [
            "2026-10-17 00:15:30.123", "2026-10-16T18:45:30.1230000Z", "Created", "identity.users.people", PersonId.ToString("D"), "Meera Nair",
            "identity.users.positions", PositionId.ToString("D"), "Vikram Iyer", AdministratorObjectId.ToString("D"), ClientAddress, CorrelationId,
        ];
        Assert.Equal(3, lines.Count);
        Assert.Equal(HeaderLine, lines[0]);
        Assert.Equal([.. shared, "departmentId", "", "", DepartmentId.ToString("D"), "Finance"], Cells(lines[1]));
        Assert.Equal([.. shared, "effectiveFrom", "", "", "2026-04-01", ""], Cells(lines[2]));
    }

    [Fact]
    public async Task WriteAsync_RecordWithoutValues_WritesOneLineWithTheValueCellsEmpty()
    {
        var lines = Lines(await WriteAsync(Record(ChangeOperation.Deleted, ChangeActorKind.Person, "Vikram Iyer")));

        var cells = Cells(Assert.Single(lines.Skip(1)));
        Assert.Equal(17, cells.Length);
        Assert.Equal("Deleted", cells[2]);
        Assert.Equal(["", "", "", "", ""], cells[12..]);
    }

    [Theory]
    [InlineData(ChangeActorKind.System, null, "The ERP")]
    [InlineData(ChangeActorKind.Anonymous, null, "Someone not signed in")]
    [InlineData(ChangeActorKind.Person, "Vikram Iyer", "Vikram Iyer")]
    [InlineData(ChangeActorKind.Person, null, "")]
    public async Task WriteAsync_KindOfActor_NamesWhoMadeTheChange(ChangeActorKind kind, string? actorName, string changedBy)
    {
        var lines = Lines(await WriteAsync(Record(ChangeOperation.Updated, kind, actorName)));

        Assert.Equal(changedBy, Cells(Assert.Single(lines.Skip(1)))[8]);
    }

    [Fact]
    public async Task WriteAsync_ValuesThatWouldBeginFormulas_WritesThemAsText()
    {
        var record = Record(
            ChangeOperation.Updated,
            ChangeActorKind.Person,
            "=cmd|' /C calc'!A0",
            new ChangedValueDetails("phoneNumber", ChangeValueKind.Text, "+919876543210", "+919812345678", null, null, null));

        var cells = Cells(Lines(await WriteAsync(record))[1]);

        Assert.Equal("'=cmd|' /C calc'!A0", cells[8]);
        Assert.Equal("'+919876543210", cells[13]);
        Assert.Equal("'+919812345678", cells[15]);
    }

    [Fact]
    public async Task WriteAsync_MoreRecordsThanAFlushHolds_FlushesAfterEveryBatchOfRecords()
    {
        var records = Enumerable.Range(0, (ChangeCsvWriter.RecordsPerFlush * 2) + 1).Select(_ => Record(ChangeOperation.Deleted, ChangeActorKind.System, null)).ToArray();
        using var body = new FlushRecordingStream();

        await ChangeCsvWriter.WriteAsync(body, Export(records), TestContext.Current.CancellationToken);

        Assert.Equal(
            [1 + ChangeCsvWriter.RecordsPerFlush, 1 + (ChangeCsvWriter.RecordsPerFlush * 2), 1 + records.Length],
            body.LinesAtEachFlush.Distinct());
    }

    [Fact]
    public async Task WriteAsync_CancelledRequest_StopsWriting()
    {
        using var body = new MemoryStream();
        var export = Export(Record(ChangeOperation.Created, ChangeActorKind.System, null));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ChangeCsvWriter.WriteAsync(body, export, new CancellationToken(canceled: true)));
    }

    [Theory]
    [InlineData("2026-10-16T18:45:30+00:00", "change-history-20261017-001530.csv")]
    [InlineData("2026-03-31T18:29:59+00:00", "change-history-20260331-235959.csv")]
    [InlineData("2026-03-31T18:30:00+00:00", "change-history-20260401-000000.csv")]
    public void FileNameOf_ExportStartedAt_NamesTheIndianTimeItStarted(string until, string fileName)
    {
        var export = new ChangeExport(0, 0, DateTimeOffset.Parse(until, CultureInfo.InvariantCulture), NoneAsync);

        Assert.Equal(fileName, ChangeCsvWriter.FileNameOf(export));
    }

    private static ChangeRecordDetails Record(ChangeOperation operation, ChangeActorKind actorKind, string? actorName, params ChangedValueDetails[] changes) =>
        new(
            Guid.CreateVersion7(),
            LateEveningUtc,
            operation,
            "identity.users.positions",
            PositionId,
            "identity.users.people",
            PersonId,
            "Meera Nair",
            actorKind,
            actorKind switch
            {
                ChangeActorKind.System => ActorIds.System,
                ChangeActorKind.Anonymous => ActorIds.Anonymous,
                _ => AdministratorObjectId,
            },
            actorName,
            ClientAddress,
            CorrelationId,
            changes);

    private static ChangeExport Export(params ChangeRecordDetails[] records) =>
        new(records.Length, records.Sum(record => Math.Max(1, record.Changes.Count)), LateEveningUtc, cancellationToken => ReadAsync(records, cancellationToken));

    private static async Task<byte[]> WriteAsync(params ChangeRecordDetails[] records)
    {
        using var body = new MemoryStream();
        await ChangeCsvWriter.WriteAsync(body, Export(records), TestContext.Current.CancellationToken);

        return body.ToArray();
    }

    private static List<string> Lines(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.EndsWith("\r\n", text, StringComparison.Ordinal);

        return [.. text[..^2].Split("\r\n")];
    }

    private static string[] Cells(string line)
    {
        Assert.StartsWith("\"", line, StringComparison.Ordinal);
        Assert.EndsWith("\"", line, StringComparison.Ordinal);

        return [.. line[1..^1].Split("\",\"").Select(cell => cell.Replace("\"\"", "\"", StringComparison.Ordinal))];
    }

    private static async IAsyncEnumerable<ChangeRecordDetails> ReadAsync(IEnumerable<ChangeRecordDetails> records, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            yield return record;
        }
    }

    private static async IAsyncEnumerable<ChangeRecordDetails> NoneAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        yield break;
    }
}
