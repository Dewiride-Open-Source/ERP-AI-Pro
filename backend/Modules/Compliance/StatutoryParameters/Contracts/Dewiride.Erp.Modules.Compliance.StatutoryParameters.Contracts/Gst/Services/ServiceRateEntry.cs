using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;

// One entry of a rate notification's Table as it prints it: the heading codes of column (2), never a six-digit code, the
// description of column (3), the rate of column (4) and the condition of column (5), null where the Table shows a dash.
public sealed record ServiceRateEntry(string Entry, string Heading, string Description, Percentage Rate, string? Condition);
