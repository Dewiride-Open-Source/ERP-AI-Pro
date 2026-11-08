using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.FinanceAct;

// Above is the amount the income or aggregate of incomes must exceed and UpTo the amount it must not exceed, both null on a cap
// or a cess; Person is null where the provision names no payee, and Percent where it adds nothing.
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
