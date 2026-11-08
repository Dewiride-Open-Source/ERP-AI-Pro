using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Limits;

// Name is the ERP's title of the case; Entry is null where the provision names more than one entry or none. A part of a serial
// number that a figure bounds alone is a case of its own, so Amount is the figure that holds for the whole of Condition: for
// serial number 7, clause (b), of the Table of section 393(4) the turnover above which a co-operative society loses its exception,
// and otherwise the most that may be paid without deduction. Count is the goods carriages of serial number 8.
public sealed record TdsNoDeductionCase(string Case, string Name, string Provisions, string? Entry, string Condition, Money? Amount, int? Count);
