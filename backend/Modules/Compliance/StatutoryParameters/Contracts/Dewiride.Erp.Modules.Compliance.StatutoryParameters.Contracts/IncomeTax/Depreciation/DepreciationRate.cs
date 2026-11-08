using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Depreciation;

// One rate of the Table in Appendix I as it prints it. Entry is the Table's own numbering: the part, the item's Roman numeral,
// then the sub-item, entry, group letter and clause as printed, such as A.III.7(ix)E(k). Heading holds the words printed
// between the item and the entry, null where there are none, and Item is null in Part B, which has no items. Rate is a
// percentage of the written down value of the block.
public sealed record DepreciationRate(string Entry, AssetPart Part, AssetItem? Item, string? Heading, string Block, Percentage Rate);
