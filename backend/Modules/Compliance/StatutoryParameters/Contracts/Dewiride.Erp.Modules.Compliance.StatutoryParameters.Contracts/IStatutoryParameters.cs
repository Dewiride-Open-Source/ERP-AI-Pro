namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;

// A set answers for the date its as-of rule names: the date of a supply, the first day of a tax period, the credit or payment at
// which tax is deducted at source, the first day of a financial year, a day for which wages are earned, the last day of a wage
// period, the day employment ends, the last day of an accounting year or the last day of a contribution period. Null means the
// ERP holds nothing for that date, because it precedes the set's first version; a set in force may hold no entries that day.
public interface IStatutoryParameters
{
    Task<TSet?> GetAsOfAsync<TSet>(DateOnly date, CancellationToken cancellationToken)
        where TSet : class, IStatutoryParameterSet<TSet>;
}
