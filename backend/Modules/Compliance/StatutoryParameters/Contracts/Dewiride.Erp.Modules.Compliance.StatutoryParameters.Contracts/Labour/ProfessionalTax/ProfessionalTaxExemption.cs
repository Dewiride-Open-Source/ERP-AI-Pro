using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProfessionalTax;

// Age, IncomeUpTo and DaysUpTo are the figures the Words' condition names, null where it names none; the Words decide the rest.
public sealed record ProfessionalTaxExemption(string Exemption, string StateCode, string Name, string Words, int? Age, ProfessionalTaxAgeTest? AgeTest, Money? IncomeUpTo, int? DaysUpTo);
