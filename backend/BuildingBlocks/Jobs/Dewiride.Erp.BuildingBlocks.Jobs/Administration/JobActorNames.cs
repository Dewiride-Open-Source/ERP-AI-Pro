using Dewiride.Erp.BuildingBlocks.Application.Actors;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Administration;

// The names of the people a page of runs or events mentions, asked for once per page from each source of people's names. A run
// and its events keep ids only, so a person is shown by the name they have today, and one no source names keeps only the id.
internal sealed class JobActorNames
{
    private readonly IReadOnlyDictionary<Guid, string> _names;

    private JobActorNames(IReadOnlyDictionary<Guid, string> names)
    {
        _names = names;
    }

    public static JobActorKind KindOf(Guid actorId) =>
        actorId == ActorIds.System ? JobActorKind.System
        : actorId == ActorIds.Anonymous ? JobActorKind.Anonymous
        : JobActorKind.Person;

    public static async Task<JobActorNames> ResolveAsync(IEnumerable<Guid> actorIds, IReadOnlyList<IPersonNames> sources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actorIds);
        ArgumentNullException.ThrowIfNull(sources);

        var people = actorIds.Where(actorId => KindOf(actorId) == JobActorKind.Person).ToHashSet();
        var names = new Dictionary<Guid, string>();
        foreach (var source in sources)
        {
            var unnamed = people.Where(person => !names.ContainsKey(person)).ToList();
            if (unnamed.Count == 0)
            {
                break;
            }

            foreach (var (person, name) in await source.DisplayNamesAsync(unnamed, cancellationToken).ConfigureAwait(false))
            {
                names.TryAdd(person, name);
            }
        }

        return new JobActorNames(names);
    }

    public JobActorDetails Of(Guid actorId)
    {
        var kind = KindOf(actorId);

        return new JobActorDetails(kind, actorId, kind == JobActorKind.Person ? _names.GetValueOrDefault(actorId) : null);
    }
}
