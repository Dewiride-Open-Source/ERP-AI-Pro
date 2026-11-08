namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Remittance;

// A charge without an entry on a day is one whose instrument for that day is not seeded, never one that is not payable, so a
// consumer refuses to compute it rather than read the gap as nothing due.
public sealed record ProvidentFundCharges(StatutoryParameterVersion Version, IReadOnlyList<ProvidentFundCharge> Charges) : IStatutoryParameterSet<ProvidentFundCharges>
{
    public static string Key => "provident-fund.charges";

    public static ProvidentFundCharges Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ProvidentFundCharge>());
}
