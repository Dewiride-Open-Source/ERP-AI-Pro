namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.FinanceAct;

// The surcharge and cess on deductions of one Finance Act, which end with the tax year it gives them for. A version in force that
// holds no clause means that year's Finance Act is not yet seeded, so a consumer refuses to compute the deduction rather than read
// the empty list as no surcharge or cess.
public sealed record TdsSurchargesAndCess(StatutoryParameterVersion Version, IReadOnlyList<TdsSurchargeOrCess> Clauses) : IStatutoryParameterSet<TdsSurchargesAndCess>
{
    public static string Key => "income-tax.tds-surcharge-and-cess";

    public static TdsSurchargesAndCess Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TdsSurchargeOrCess>());
}
