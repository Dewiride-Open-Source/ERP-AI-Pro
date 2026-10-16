namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

// The building block or module that owns a recorded type names its rows for the readers of the change records, by change
// key, so a record shows a person or a department as it is named today. An id without a name is left out of the answer.
public interface IRecordNames
{
    IReadOnlySet<string> EntityTypes { get; }

    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
