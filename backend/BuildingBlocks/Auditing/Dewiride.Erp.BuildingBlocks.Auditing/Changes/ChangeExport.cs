namespace Dewiride.Erp.BuildingBlocks.Auditing.Changes;

// An export holds the records made up to the moment it started, so a change saved while it is read stays out of it. Its size
// is counted in the lines a CSV of it holds, one per changed value and one for a record without any, because a created record
// carries every field it was created with.
public sealed record ChangeExport(long RecordCount, long LineCount, DateTimeOffset Until, Func<CancellationToken, IAsyncEnumerable<ChangeRecordDetails>> Read)
{
    public const int MaxLines = 100_000;
}
