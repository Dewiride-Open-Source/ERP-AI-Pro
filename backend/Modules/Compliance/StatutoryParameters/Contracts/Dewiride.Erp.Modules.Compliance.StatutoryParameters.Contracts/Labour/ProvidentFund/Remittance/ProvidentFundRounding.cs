namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Remittance;

public sealed record ProvidentFundRounding(StatutoryParameterVersion Version, IReadOnlyList<ContributionRounding> Contributions) : IStatutoryParameterSet<ProvidentFundRounding>
{
    public static string Key => "provident-fund.rounding";

    public static ProvidentFundRounding Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ContributionRounding>());
}
