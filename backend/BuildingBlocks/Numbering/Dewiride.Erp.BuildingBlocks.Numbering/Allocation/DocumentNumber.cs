using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Allocation;

// The number a document carries, unique within its financial year, and its running number, by which the year's documents sort
// and a return states the first and last number issued.
public sealed record DocumentNumber(string Text, FinancialYear Year, long Sequence);
