namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.FinanceAct;

public sealed record TdsRateInForceReading(string Reading, string Entry, TdsPayee Payee, string Item, string Why);
