namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Remittance;

// A remittance without an entry on a day is one whose instrument for that day is not seeded, never one without a due day, so a
// consumer refuses to schedule it rather than read the gap as no deadline.
public sealed record ProvidentFundDueDates(StatutoryParameterVersion Version, IReadOnlyList<ProvidentFundDueDate> Remittances) : IStatutoryParameterSet<ProvidentFundDueDates>
{
    public static string Key => "provident-fund.due-dates";

    public static ProvidentFundDueDates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ProvidentFundDueDate>());
}
