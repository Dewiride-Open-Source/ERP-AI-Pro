using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.FinanceAct;

public sealed record TdsRateInForce(string Item, TdsPayee Payee, string Income, Percentage Percent);
