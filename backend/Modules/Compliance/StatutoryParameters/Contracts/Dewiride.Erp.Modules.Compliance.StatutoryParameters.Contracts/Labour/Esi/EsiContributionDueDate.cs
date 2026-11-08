namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

// DaysAfterMonthEnd counts from the last day of the calendar month in which the contributions fall due; it is null on a rule whose
// Wording names the day itself.
public sealed record EsiContributionDueDate(string Rule, string Name, int? DaysAfterMonthEnd, string Wording);
