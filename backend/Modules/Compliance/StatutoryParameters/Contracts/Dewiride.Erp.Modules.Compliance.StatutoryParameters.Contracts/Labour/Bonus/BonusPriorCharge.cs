using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Bonus;

// Category is empty only for the Explanation of Appendix D, which says what the reserves of three categories' items are.
public sealed record BonusPriorCharge(
    string Item,
    string Name,
    BonusEmployerCategory? Category,
    Percentage? Percent,
    Money? Amount,
    string Wording);
