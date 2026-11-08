namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Codes;

// The payments a Code excludes by clauses (a) to (i) are added back to its wages for the part by which they exceed its first
// proviso's per cent of all remuneration; those of clauses (j) and (k) never are. Each Code's clauses answer for that Code alone.
public sealed record WageDefinition(StatutoryParameterVersion Version, IReadOnlyList<WageDefinitionClause> Clauses) : IStatutoryParameterSet<WageDefinition>
{
    public static string Key => "labour-codes.wage-definition";

    public static WageDefinition Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<WageDefinitionClause>());
}
