namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Entries;

// Every text is the Act's own words. Heading is the group heading of the Table of section 393(1), null elsewhere; Payee is column C
// of the Table of section 393(2), the payee the opening words of section 393(1) or 393(3) name, and null for section 392; Threshold
// is null where the provision prints none, as no entry of the Table of section 393(2) does.
public sealed record TdsEntry(
    string Entry,
    string Citation,
    TdsTable Table,
    string? Heading,
    string Nature,
    string? Payee,
    string Payer,
    string Rate,
    string? Threshold,
    TdsTiming Timing,
    string TimingWords,
    string? Notes);
