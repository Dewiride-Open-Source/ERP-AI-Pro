namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Crosswalk;

// NewEntry is the 2025 TDS entry a deduction pair leads to, the code of NewProvision, and null on any other pair; OldDescription
// is the text TRACES shows for the 1961 provision, which is not always the provision's heading, null where neither of its pages
// names it.
public sealed record TdsCrosswalkPair(
    string Pair,
    string OldProvision,
    string? OldDescription,
    string? NewProvision,
    string? NewEntry,
    string? NewCitation,
    string Payment,
    string? Part,
    TdsCrosswalkKind Kind,
    TdsCrosswalkTie Tie,
    string Why);
