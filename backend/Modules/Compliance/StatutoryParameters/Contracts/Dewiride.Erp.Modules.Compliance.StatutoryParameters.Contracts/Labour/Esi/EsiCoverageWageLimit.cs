using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

// Amount is the wage limit a month; it is null on the continued coverage, a rule about the limit whose Wording decides.
public sealed record EsiCoverageWageLimit(string Limit, string Name, Money? Amount, string Wording);
