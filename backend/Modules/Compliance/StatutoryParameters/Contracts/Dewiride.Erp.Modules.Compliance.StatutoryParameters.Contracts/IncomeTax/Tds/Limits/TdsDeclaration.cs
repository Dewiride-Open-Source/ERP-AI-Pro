namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Limits;

// Age is the age from which a resident individual is outside the limit of the Note below the Table of section 393(6), null in
// every other row.
public sealed record TdsDeclaration(string Row, string Person, string Provisions, int? Age);
