namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.WelfareFund;

// A State without a definition is one whose Act the ERP has not read in full, never one whose fund covers no one.
public sealed record WelfareFundCoverage(StatutoryParameterVersion Version, IReadOnlyList<WelfareFundCoverageDefinition> Definitions) : IStatutoryParameterSet<WelfareFundCoverage>
{
    public static string Key => "labour-welfare-fund.coverage";

    public static WelfareFundCoverage Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<WelfareFundCoverageDefinition>());
}
