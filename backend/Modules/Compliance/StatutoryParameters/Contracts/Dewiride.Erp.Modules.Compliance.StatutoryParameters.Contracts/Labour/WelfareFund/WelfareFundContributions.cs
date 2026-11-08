namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.WelfareFund;

// A State without a contribution is one whose instrument the ERP has not read, never one without a fund, so a consumer refuses to
// compute a State's contribution rather than read no row as nothing due.
public sealed record WelfareFundContributions(StatutoryParameterVersion Version, IReadOnlyList<WelfareFundContribution> Contributions) : IStatutoryParameterSet<WelfareFundContributions>
{
    public static string Key => "labour-welfare-fund.contributions";

    public static WelfareFundContributions Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<WelfareFundContribution>());
}
