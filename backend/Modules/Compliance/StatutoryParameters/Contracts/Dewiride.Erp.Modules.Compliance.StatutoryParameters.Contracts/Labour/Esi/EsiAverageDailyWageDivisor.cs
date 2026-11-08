namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

// Days divides the wage of the wage period into its average daily wages; it is null where the days worked in the wage period divide
// it instead.
public sealed record EsiAverageDailyWageDivisor(string Divisor, string Name, int? Days, string Wording);
