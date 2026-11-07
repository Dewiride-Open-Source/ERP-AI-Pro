using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Timeline;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.PublicApi;

// A set's history is computed in memory from all of its versions and rows, which suits sets of hundreds of rows; a set of many
// thousand would need its answers paged.
internal sealed class StatutoryParameterHistory(StatutoryParametersDbContext context) : IStatutoryParameterHistory
{
    public async Task<IReadOnlyList<ParameterSetEntry>> ListSetsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var sets = await context.ParameterSets.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var versions = (await context.ParameterVersions.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false)).ToLookup(version => version.SetId);
        var rows = (await context.ParameterRows.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false)).ToLookup(row => row.SetId);

        return [.. sets
            .Where(set => versions[set.Id].Any())
            .Select(set => EntryOf(set, versions[set.Id].ToList(), rows[set.Id], today))
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)];
    }

    public async Task<ParameterSetDetails?> GetSetAsync(string key, DateOnly today, CancellationToken cancellationToken)
    {
        if (await LoadAsync(key, cancellationToken).ConfigureAwait(false) is not { } loaded)
        {
            return null;
        }

        var (set, versions, timeline) = loaded;

        return new ParameterSetDetails(
            set.Key,
            set.Title,
            set.Description,
            HistoryEntries.BasisOf(set.Basis),
            HistoryEntries.AsOfOf(set.AsOf),
            [.. set.Columns.Columns.Select(HistoryEntries.ColumnOf)],
            [.. timeline.Versions.Reverse().Select(from => HistoryEntries.VersionOf(versions[from], timeline))],
            timeline.VersionOn(today));
    }

    public async Task<ParameterVersionView?> GetVersionAsync(string key, DateOnly on, CancellationToken cancellationToken)
    {
        if (await LoadAsync(key, cancellationToken).ConfigureAwait(false) is not { } loaded || loaded.Timeline.VersionOn(on) is not { } from)
        {
            return null;
        }

        var (set, versions, timeline) = loaded;
        var changes = timeline.ChangesAt(from);

        return new ParameterVersionView(
            from,
            timeline.UntilOf(from),
            versions[from].Instruments,
            [.. timeline.RowsOn(from).Select(row => HistoryEntries.RowOf(set, row))],
            [.. changes.Added.Select(row => HistoryEntries.RowOf(set, row))],
            [.. changes.Changed.Select(change => new ParameterRowChange(HistoryEntries.RowOf(set, change.Before), HistoryEntries.RowOf(set, change.After)))],
            [.. changes.Removed.Select(row => HistoryEntries.RowOf(set, row))]);
    }

    private static ParameterSetEntry EntryOf(ParameterSet set, List<ParameterVersion> versions, IEnumerable<ParameterRow> rows, DateOnly today)
    {
        var timeline = new ParameterTimeline(versions.Select(version => version.From), rows);
        var byDate = versions.ToDictionary(version => version.From);
        var current = timeline.VersionOn(today);
        var next = timeline.NextAfter(today);

        return new ParameterSetEntry(
            set.Key,
            set.Title,
            set.Description,
            HistoryEntries.BasisOf(set.Basis),
            HistoryEntries.AsOfOf(set.AsOf),
            timeline.Versions[0],
            timeline.Versions.Count,
            current is { } from ? HistoryEntries.VersionOf(byDate[from], timeline) : null,
            next is { } upcoming ? HistoryEntries.VersionOf(byDate[upcoming], timeline) : null,
            current is null ? 0 : timeline.RowsOn(today).Count);
    }

    private async Task<(ParameterSet Set, Dictionary<DateOnly, ParameterVersion> Versions, ParameterTimeline Timeline)?> LoadAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!ParameterSet.IsKey(key))
        {
            return null;
        }

        var set = await context.ParameterSets.AsNoTracking().SingleOrDefaultAsync(s => s.Key == key, cancellationToken).ConfigureAwait(false);
        if (set is null)
        {
            return null;
        }

        var versions = await context.ParameterVersions.AsNoTracking().Where(v => v.SetId == set.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var rows = await context.ParameterRows.AsNoTracking().Where(r => r.SetId == set.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (versions.Count == 0)
        {
            return null;
        }

        return (set, versions.ToDictionary(version => version.From), new ParameterTimeline(versions.Select(version => version.From), rows));
    }
}
