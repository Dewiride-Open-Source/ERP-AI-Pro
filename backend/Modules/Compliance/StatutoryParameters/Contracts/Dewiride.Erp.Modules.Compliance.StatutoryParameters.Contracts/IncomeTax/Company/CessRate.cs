using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Company;

public sealed record CessRate(string Cess, string Name, Percentage Rate, string Wording);
