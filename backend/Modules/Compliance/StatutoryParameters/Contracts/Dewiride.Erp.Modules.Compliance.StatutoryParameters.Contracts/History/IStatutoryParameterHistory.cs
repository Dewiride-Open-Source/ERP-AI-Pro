namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;

// Every set read as data, whatever its shape: its definition, its versions newest first with what each added, changed and
// removed, and the entries of the version in force on a date. A version runs until the day before the next one, and the
// entries of a version are the same on every day it runs.
public interface IStatutoryParameterHistory
{
    Task<IReadOnlyList<ParameterSetEntry>> ListSetsAsync(DateOnly today, CancellationToken cancellationToken);

    Task<ParameterSetDetails?> GetSetAsync(string key, DateOnly today, CancellationToken cancellationToken);

    Task<ParameterVersionView?> GetVersionAsync(string key, DateOnly on, CancellationToken cancellationToken);
}
