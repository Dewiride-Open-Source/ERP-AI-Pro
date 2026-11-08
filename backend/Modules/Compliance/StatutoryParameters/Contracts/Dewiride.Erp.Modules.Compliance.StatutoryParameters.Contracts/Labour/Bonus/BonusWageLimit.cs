using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Bonus;

public sealed record BonusWageLimit(
    string Government,
    string Name,
    string? StateCode,
    Money EligibilityLimit,
    Money CalculationWage,
    string EligibilityWording,
    string CalculationWording);
