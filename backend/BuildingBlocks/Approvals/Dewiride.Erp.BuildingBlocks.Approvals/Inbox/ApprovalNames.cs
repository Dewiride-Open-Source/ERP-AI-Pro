using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

// The names a page shows, asked for once per page: one question to the names of each kind of record its requests approve, and one
// to each source of people's names for the people it shows, so a person is shown by the name they have today. A request keeps ids
// only, and a name no source gives stays null.
internal sealed class ApprovalNames
{
    private readonly Dictionary<string, IReadOnlyDictionary<Guid, string>> _records;

    private readonly Dictionary<Guid, string> _people;

    private ApprovalNames(Dictionary<string, IReadOnlyDictionary<Guid, string>> records, Dictionary<Guid, string> people)
    {
        _records = records;
        _people = people;
    }

    public static async Task<ApprovalNames> ResolveAsync(
        IReadOnlyCollection<(string Type, Guid Id)> records,
        IReadOnlyCollection<Guid> people,
        IEnumerable<IRecordNames> recordNames,
        IEnumerable<IPersonNames> personNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(recordNames);
        ArgumentNullException.ThrowIfNull(personNames);

        var named = new Dictionary<string, IReadOnlyDictionary<Guid, string>>(StringComparer.Ordinal);
        var recordSources = recordNames.ToList();
        foreach (var type in records.GroupBy(record => record.Type, StringComparer.Ordinal))
        {
            if (recordSources.Find(source => source.EntityTypes.Contains(type.Key)) is { } source)
            {
                named[type.Key] = await source.NamesAsync(type.Key, [.. type.Select(record => record.Id).Distinct()], cancellationToken).ConfigureAwait(false);
            }
        }

        var names = new Dictionary<Guid, string>();
        var wanted = people.Where(person => person != ActorIds.System && person != ActorIds.Anonymous).Distinct().ToList();
        foreach (var source in personNames)
        {
            var unnamed = wanted.Where(person => !names.ContainsKey(person)).ToList();
            if (unnamed.Count == 0)
            {
                break;
            }

            foreach (var (person, name) in await source.DisplayNamesAsync(unnamed, cancellationToken).ConfigureAwait(false))
            {
                names.TryAdd(person, name);
            }
        }

        return new ApprovalNames(named, names);
    }

    public string? RecordName(string type, Guid id) => _records.TryGetValue(type, out var names) && names.TryGetValue(id, out var name) ? name : null;

    public string? PersonName(Guid? person) => person is { } id && _people.TryGetValue(id, out var name) ? name : null;
}
