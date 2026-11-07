namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;

// A set answers for the date its as-of rule names: the date of a supply, or the first day of a tax period. Null means the ERP
// holds nothing for that date, because it precedes the set's first version; a set in force may hold no entries that day.
public interface IStatutoryParameters
{
    Task<TSet?> GetAsOfAsync<TSet>(DateOnly date, CancellationToken cancellationToken)
        where TSet : class, IStatutoryParameterSet<TSet>;
}
