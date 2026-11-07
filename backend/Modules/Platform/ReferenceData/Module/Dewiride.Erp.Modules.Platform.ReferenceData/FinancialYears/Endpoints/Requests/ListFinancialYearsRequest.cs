using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Application;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Endpoints.Requests;

public sealed record ListFinancialYearsRequest(
    [property: FromQuery(Name = "from")]
    [property: Description("First financial year to list, by its label such as 2024-25.")]
    [property: Required]
    [property: StringLength(FinancialYearLimits.LabelLength)]
    [property: RegularExpression(FinancialYearLimits.LabelPattern)]
    string? From,
    [property: FromQuery(Name = "to")]
    [property: Description("Last financial year to list, by its label such as 2027-28; at most 49 years after the first.")]
    [property: Required]
    [property: StringLength(FinancialYearLimits.LabelLength)]
    [property: RegularExpression(FinancialYearLimits.LabelPattern)]
    string? To);
