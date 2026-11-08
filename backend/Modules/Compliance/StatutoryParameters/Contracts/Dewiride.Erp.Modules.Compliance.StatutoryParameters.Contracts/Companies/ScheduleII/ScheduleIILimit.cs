using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Companies.ScheduleII;

// Percent is of what Test names: the most a residual value may be, as a share of the asset's original cost; the increase in
// depreciation for a period of double-shift working; and the percentage on whose basis depreciation is calculated for a period
// of triple-shift working.
public sealed record ScheduleIILimit(string Limit, string Name, Percentage Percent, string Test);
