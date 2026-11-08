using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Bonus;

public sealed record StateWageLimitReading(
    string StateCode,
    Money EligibilityLimit,
    Money CalculationWage,
    string EligibilityWording,
    string CalculationWording,
    string Why);
