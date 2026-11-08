namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Crosswalk;

// NewEntry is the 2025 TDS entry a deduction pair leads to, the code of NewProvision, and null on any other pair; Heading1961 is
// the text TRACES shows for the 1961 provision, null where neither of its pages names it.
public sealed record TdsCrosswalkPair(
    string Pair,
    string OldProvision,
    string? Heading1961,
    string? NewProvision,
    string? NewEntry,
    string? NewCitation,
    string Payment,
    string? Part,
    TdsCrosswalkKind Kind,
    TdsCrosswalkTie Tie,
    string Why);
