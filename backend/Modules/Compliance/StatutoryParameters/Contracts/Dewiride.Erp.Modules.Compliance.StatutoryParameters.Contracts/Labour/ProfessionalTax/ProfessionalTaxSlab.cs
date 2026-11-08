using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProfessionalTax;

// A band holds a value more than Above, at least AtLeast, up to UpTo and less than Below, a null bound being none, so a value the
// printed bounds leave between two bands is in none. Monthly and Yearly are the tax as the entry states it, both 0 for a slab
// printed Nil or Zero, and OtherMonth, 1 to 12, is the month of the year whose tax is OtherMonthTax instead of Monthly.
public sealed record ProfessionalTaxSlab(
    string Slab,
    string StateCode,
    string Name,
    string? Earners,
    string? Band,
    string Rate,
    ProfessionalTaxSlabMeasure? Measure,
    Money? Above,
    Money? AtLeast,
    Money? UpTo,
    Money? Below,
    Money? Monthly,
    Money? Yearly,
    int? OtherMonth,
    Money? OtherMonthTax,
    Money? LumpSum,
    string? Explanations);
