namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.WelfareFund;

// A State without a due day is one whose days the ERP has not read, never one where nothing falls due.
public sealed record WelfareFundDueDays(StatutoryParameterVersion Version, IReadOnlyList<WelfareFundDueDay> Days) : IStatutoryParameterSet<WelfareFundDueDays>
{
    public static string Key => "labour-welfare-fund.due-days";

    public static WelfareFundDueDays Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<WelfareFundDueDay>());
}
