using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Limits;

// Name is the ERP's title of the term; Amount and Age are the figures the definition sets, null where it sets none, and Test is
// how a turnover or an age is compared with them.
public sealed record TdsTerm(string Term, string Name, Money? Amount, int? Age, TdsTermTest? Test, string Words);
