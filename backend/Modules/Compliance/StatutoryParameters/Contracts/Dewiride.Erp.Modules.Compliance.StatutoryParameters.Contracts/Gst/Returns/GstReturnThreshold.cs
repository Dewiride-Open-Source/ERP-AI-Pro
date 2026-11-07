using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Returns;

public sealed record GstReturnThreshold(string Threshold, string Name, Money Amount, string Test);
