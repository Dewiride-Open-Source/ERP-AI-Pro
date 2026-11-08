using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Bonus;

// Eight and one-third per cent. has no decimal of four places, so a rule that names it holds PercentNumerator over
// PercentDenominator per cent. and leaves Percent empty; a consumer multiplies by the numerator before it divides. Bound says how
// the rule's count binds or, in a rule without a count, its per cent.
public sealed record BonusRule(
    string Provision,
    string Name,
    Percentage? Percent,
    int? PercentNumerator,
    int? PercentDenominator,
    Money? Amount,
    int? Count,
    BonusUnit? Unit,
    BonusBound? Bound,
    DayAndMonth? Day,
    string Wording);
