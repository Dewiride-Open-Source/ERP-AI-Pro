namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProfessionalTax;

public sealed record ProfessionalTaxLevy(string StateCode, string Name, ProfessionalTaxLevyStatus Status, string Law, string Words, string? Extent, string? Salary);
