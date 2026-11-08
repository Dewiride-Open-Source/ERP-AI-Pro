namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Remittance;

// Days counts from the close of the wage month, or is a day of the month that follows it, as Counted says.
public sealed record ProvidentFundDueDate(string Remittance, string Name, int Days, DueDayCounting Counted, string Wording);
