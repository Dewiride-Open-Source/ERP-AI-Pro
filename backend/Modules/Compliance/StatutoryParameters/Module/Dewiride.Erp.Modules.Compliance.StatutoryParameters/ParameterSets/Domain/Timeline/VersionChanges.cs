namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Timeline;

// What a version changed, matched by key: a key whose row starts on the version's date and had none the day before is added,
// one that had a row ending the day before is changed, and a key whose row ended the day before with none starting is removed.
internal sealed record VersionChanges(IReadOnlyList<ParameterRow> Added, IReadOnlyList<RowChange> Changed, IReadOnlyList<ParameterRow> Removed)
{
    public bool IsEmpty => Added.Count == 0 && Changed.Count == 0 && Removed.Count == 0;
}
