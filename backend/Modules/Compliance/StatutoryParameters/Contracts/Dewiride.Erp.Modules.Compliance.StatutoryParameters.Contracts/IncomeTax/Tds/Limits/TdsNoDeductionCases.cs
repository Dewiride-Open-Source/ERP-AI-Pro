namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Limits;

public sealed record TdsNoDeductionCases(StatutoryParameterVersion Version, IReadOnlyList<TdsNoDeductionCase> Cases) : IStatutoryParameterSet<TdsNoDeductionCases>
{
    public static string Key => "income-tax.tds-no-deduction-cases";

    public static TdsNoDeductionCases Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TdsNoDeductionCase>());
}
