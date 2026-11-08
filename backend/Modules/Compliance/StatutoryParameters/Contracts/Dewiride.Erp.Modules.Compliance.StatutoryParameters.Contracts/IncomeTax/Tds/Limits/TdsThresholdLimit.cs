using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Limits;

// Amount is 0 for a limit the Act prints as Nil, which every amount exceeds, and null, with Measure, for a limit printed without a
// figure, whose Words decide.
public sealed record TdsThresholdLimit(string Limit, string Entry, string? Case, string Words, Money? Amount, TdsLimitTest Test, TdsLimitMeasure? Measure);
