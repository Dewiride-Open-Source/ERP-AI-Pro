using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

// AverageDailyWagesUpTo is compared with the average daily wages during the wage period, reckoned by EsiAverageDailyWageDivisors,
// and includes its own amount; Years counts from the commencement of the contribution period.
public sealed record EsiContributionExemption(string Exemption, string Name, Money? AverageDailyWagesUpTo, int? Years, string Wording);
