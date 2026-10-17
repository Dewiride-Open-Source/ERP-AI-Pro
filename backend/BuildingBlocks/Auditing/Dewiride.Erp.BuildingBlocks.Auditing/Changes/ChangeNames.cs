using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Changes;

// The names a page of change records shows, asked for once per page: one question to the names of each change key the page
// files a record under or references, and one to each source of people's names for the people who made the changes. A record
// stores ids only for its actor, so a person is shown by the name they have today. A name no source gives stays empty, and
// the record still shows its ids.
internal sealed class ChangeNames
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<Guid, string>> _records;

    private readonly IReadOnlyDictionary<Guid, string> _people;

    private ChangeNames(IReadOnlyDictionary<string, IReadOnlyDictionary<Guid, string>> records, IReadOnlyDictionary<Guid, string> people)
    {
        _records = records;
        _people = people;
    }

    public static ChangeActorKind ActorKindOf(Guid actorId) =>
        actorId == ActorIds.System ? ChangeActorKind.System
        : actorId == ActorIds.Anonymous ? ChangeActorKind.Anonymous
        : ChangeActorKind.Person;

    public static async Task<ChangeNames> ResolveAsync(
        IReadOnlyList<(ChangeRecord Record, IReadOnlyList<StoredChange> Values)> page,
        IReadOnlyDictionary<string, IRecordNames> recordNames,
        IReadOnlyList<IPersonNames> personNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(recordNames);
        ArgumentNullException.ThrowIfNull(personNames);

        var wanted = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);
        var actors = new HashSet<Guid>();
        foreach (var (record, values) in page)
        {
            Want(wanted, record.SubjectType, record.SubjectId);
            if (ActorKindOf(record.ActorId) == ChangeActorKind.Person)
            {
                actors.Add(record.ActorId);
            }

            foreach (var value in values.Where(value => value.Kind == ChangeValueKind.Reference && value.References is not null))
            {
                Want(wanted, value.References!, value.Before);
                Want(wanted, value.References!, value.After);
            }
        }

        var records = new Dictionary<string, IReadOnlyDictionary<Guid, string>>(StringComparer.Ordinal);
        foreach (var (key, ids) in wanted)
        {
            if (recordNames.TryGetValue(key, out var names))
            {
                records[key] = await names.NamesAsync(key, ids, cancellationToken).ConfigureAwait(false);
            }
        }

        var people = new Dictionary<Guid, string>();
        foreach (var source in personNames)
        {
            var unnamed = actors.Where(actor => !people.ContainsKey(actor)).ToList();
            if (unnamed.Count == 0)
            {
                break;
            }

            foreach (var (actor, name) in await source.DisplayNamesAsync(unnamed, cancellationToken).ConfigureAwait(false))
            {
                people.TryAdd(actor, name);
            }
        }

        return new ChangeNames(records, people);
    }

    public string? RecordName(string key, Guid id) =>
        _records.TryGetValue(key, out var names) && names.TryGetValue(id, out var name) ? name : null;

    public string? RecordName(string? key, string? id) =>
        key is not null && Guid.TryParse(id, out var parsed) ? RecordName(key, parsed) : null;

    public string? PersonName(Guid actorId) => _people.TryGetValue(actorId, out var name) ? name : null;

    private static void Want(Dictionary<string, HashSet<Guid>> wanted, string key, Guid id)
    {
        if (!wanted.TryGetValue(key, out var ids))
        {
            ids = [];
            wanted.Add(key, ids);
        }

        ids.Add(id);
    }

    private static void Want(Dictionary<string, HashSet<Guid>> wanted, string key, string? id)
    {
        if (Guid.TryParse(id, out var parsed))
        {
            Want(wanted, key, parsed);
        }
    }
}
