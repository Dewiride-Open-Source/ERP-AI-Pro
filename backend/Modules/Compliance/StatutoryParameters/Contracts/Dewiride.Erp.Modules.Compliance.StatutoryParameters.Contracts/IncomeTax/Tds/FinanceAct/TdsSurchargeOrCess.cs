using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.FinanceAct;

// Above is the amount the income or aggregate of incomes must exceed and UpTo the amount it must not exceed, both null on any row
// but a band; Person is null where the provision names no payee, and Percent where the row states no rate of its own, as on the
// clause that adds no cess and on section 3(8), which sends its collections to the rates and surcharge of Part II.
public sealed record TdsSurchargeOrCess(
    string Clause,
    TdsSurchargeProvision Provision,
    string? Person,
    string AppliesTo,
    TdsSurchargeKind Kind,
    Money? Above,
    Money? UpTo,
    TdsSurchargeScope? Scope,
    Percentage? Percent,
    string Words);
