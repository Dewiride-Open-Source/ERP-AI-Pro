namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;

// A typed set names the seeded set it reads by its key, as an integration event names itself, and builds itself from the
// version in force on a date and that version's rows.
public interface IStatutoryParameterSet<TSelf>
    where TSelf : class, IStatutoryParameterSet<TSelf>
{
    static abstract string Key { get; }

    static abstract TSelf Create(StatutoryParameterVersion version, IStatutoryParameterRows rows);
}
