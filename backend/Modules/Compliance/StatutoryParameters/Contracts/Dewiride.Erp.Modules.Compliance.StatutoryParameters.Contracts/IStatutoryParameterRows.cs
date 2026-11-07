namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;

// The rows of the version a set record is built from, read into the record's row type by the columns the set declares: a
// percent as Percentage, an amount as Money in INR and a choice as the enum member of its camelCase value.
public interface IStatutoryParameterRows
{
    IReadOnlyList<TRow> Read<TRow>();
}
