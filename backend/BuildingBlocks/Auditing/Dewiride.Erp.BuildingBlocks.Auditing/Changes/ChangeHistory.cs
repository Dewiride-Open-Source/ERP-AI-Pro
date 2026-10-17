using System.Runtime.CompilerServices;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Changes;

// The history sorts and filters on a record's own columns only, never inside its values, so an index of audit.ChangeRecords
// serves every list and export. An export reads oldest first in pages that each start after the time the page before ended;
// a page that ends inside a run of records of one time also takes the rest of that run, so no query compares ids and no record
// committed before the export began is read twice or skipped. Each page is a query of its own, which the retrying execution
// strategy buffers anyway.
internal sealed class ChangeHistory : IChangeHistory
{
    public const int ExportPageSize = 500;

    private static readonly SortableFields<ChangeRecord> Sortable = new SortableFields<ChangeRecord>()
        .Add("occurredAt", r => r.OccurredAt);

    private static readonly FilterableFields<ChangeRecord> Filterable = new FilterableFields<ChangeRecord>()
        .Add("entityType", r => r.EntityType, FilterOperators.Equality)
        .Add("entityId", r => r.EntityId, FilterOperators.Equality)
        .Add("subjectType", r => r.SubjectType, FilterOperators.Equality)
        .Add("subjectId", r => r.SubjectId, FilterOperators.Equality)
        .Add("actorId", r => r.ActorId, FilterOperators.Equality)
        .Add("operation", r => r.Operation, FilterOperators.Equality)
        .Add("correlationId", r => r.CorrelationId, FilterOperators.Equality)
        .Add("occurredAt", r => r.OccurredAt, FilterOperators.Comparable);

    private static readonly SortRequest NewestFirst = new([new SortTerm("occurredAt", SortDirection.Descending)]);

    private readonly AuditingDbContext _context;

    private readonly IReadOnlyDictionary<string, IRecordNames> _recordNames;

    private readonly IReadOnlyList<IPersonNames> _personNames;

    private readonly TimeProvider _timeProvider;

    public ChangeHistory(AuditingDbContext context, IEnumerable<IRecordNames> recordNames, IEnumerable<IPersonNames> personNames, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(recordNames);
        ArgumentNullException.ThrowIfNull(personNames);

        var named = new Dictionary<string, IRecordNames>(StringComparer.Ordinal);
        foreach (var names in recordNames)
        {
            foreach (var key in names.EntityTypes)
            {
                if (!named.TryAdd(key, names))
                {
                    throw new InvalidOperationException($"The change key {key} is named by both {named[key].GetType().Name} and {names.GetType().Name}.");
                }
            }
        }

        _context = context;
        _recordNames = named;
        _personNames = [.. personNames];
        _timeProvider = timeProvider;
    }

    public async Task<Result<PagedResult<ChangeRecordDetails>>> ListAsync(ListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sort = (request.Sort.Terms.Count == 0 ? NewestFirst : request.Sort).Resolve(Sortable);
        if (sort.IsFailure)
        {
            return sort.Error!;
        }

        var filter = request.Filter.Resolve(Filterable);
        if (filter.IsFailure)
        {
            return filter.Error!;
        }

        var page = await _context.ChangeRecords
            .AsNoTracking()
            .ApplyFilter(filter.Value)
            .ApplySort(sort.Value, r => r.Id)
            .ToPagedResultAsync(request.Page, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ChangeRecordDetails>(await DetailsAsync(page.Items, cancellationToken).ConfigureAwait(false), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<Result<ChangeExport>> ExportAsync(FilterRequest filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var resolved = filter.Resolve(Filterable);
        if (resolved.IsFailure)
        {
            return resolved.Error!;
        }

        var until = _timeProvider.GetUtcNow();
        var records = Exported(resolved.Value, until);
        var recordCount = await records.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var lineCount = await records.SumAsync(r => (long?)(r.ValueCount > 1 ? r.ValueCount : 1), cancellationToken).ConfigureAwait(false) ?? 0;
        if (lineCount > ChangeExport.MaxLines)
        {
            return ChangeHistoryErrors.ExportTooLarge;
        }

        return new ChangeExport(recordCount, lineCount, until, token => ReadAsync(resolved.Value, until, token));
    }

    private IQueryable<ChangeRecord> Exported(ResolvedFilter<ChangeRecord> filter, DateTimeOffset until) =>
        _context.ChangeRecords.AsNoTracking().ApplyFilter(filter).Where(r => r.OccurredAt <= until);

    private async IAsyncEnumerable<ChangeRecordDetails> ReadAsync(ResolvedFilter<ChangeRecord> filter, DateTimeOffset until, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var records = Exported(filter, until);
        DateTimeOffset? endedAt = null;
        while (true)
        {
            var page = await (endedAt is { } start ? records.Where(r => r.OccurredAt > start) : records)
                .OrderBy(r => r.OccurredAt)
                .ThenBy(r => r.Id)
                .Take(ExportPageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (page.Count == 0)
            {
                yield break;
            }

            var full = page.Count == ExportPageSize;
            if (full)
            {
                var last = page[^1].OccurredAt;
                var read = page.Where(r => r.OccurredAt == last).Select(r => r.Id).ToHashSet();
                var run = await records.Where(r => r.OccurredAt == last).OrderBy(r => r.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
                page.AddRange(run.Where(r => !read.Contains(r.Id)));
            }

            foreach (var details in await DetailsAsync(page, cancellationToken).ConfigureAwait(false))
            {
                yield return details;
            }

            if (!full)
            {
                yield break;
            }

            endedAt = page[^1].OccurredAt;
        }
    }

    private async Task<IReadOnlyList<ChangeRecordDetails>> DetailsAsync(IReadOnlyList<ChangeRecord> records, CancellationToken cancellationToken)
    {
        var page = records.Select(record => (Record: record, Values: StoredChanges.Parse(record.Changes))).ToList();
        var names = await ChangeNames.ResolveAsync(page, _recordNames, _personNames, cancellationToken).ConfigureAwait(false);

        return [.. page.Select(entry => Details(entry.Record, entry.Values, names))];
    }

    private static ChangeRecordDetails Details(ChangeRecord record, IReadOnlyList<StoredChange> values, ChangeNames names)
    {
        var actorKind = ChangeNames.ActorKindOf(record.ActorId);

        return new ChangeRecordDetails(
            record.Id.Value,
            record.OccurredAt,
            record.Operation,
            record.EntityType,
            record.EntityId,
            record.SubjectType,
            record.SubjectId,
            names.RecordName(record.SubjectType, record.SubjectId),
            actorKind,
            record.ActorId,
            actorKind == ChangeActorKind.Person ? names.PersonName(record.ActorId) : null,
            record.ClientAddress,
            record.CorrelationId,
            [.. values.Select(value => Details(value, names))]);
    }

    private static ChangedValueDetails Details(StoredChange value, ChangeNames names)
    {
        var isReference = value.Kind == ChangeValueKind.Reference;

        return new ChangedValueDetails(
            value.Property,
            value.Kind,
            value.Before,
            value.After,
            value.References,
            isReference ? names.RecordName(value.References, value.Before) : null,
            isReference ? names.RecordName(value.References, value.After) : null);
    }
}
