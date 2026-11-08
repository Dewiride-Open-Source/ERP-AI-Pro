using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Requests;

public sealed record NumberFormatRequest(
    [property: Description("Fixed text every number begins with: capital letters, digits and the special characters the rule allows, such as INV/; empty for none.")]
    [property: Required(AllowEmptyStrings = true)]
    [property: StringLength(NumberFormat.PrefixMaxLength)]
    string? Prefix,
    [property: Description("How the financial year follows the fixed text: none, full (2026-27) or abbreviated (26-27).")]
    [property: Required]
    NumberYearStyle? Year,
    [property: Description("Character between the financial year and the running number, such as /; empty for none, and empty when the year is not shown.")]
    [property: Required(AllowEmptyStrings = true)]
    [property: StringLength(NumberFormat.SeparatorMaxLength)]
    string? Separator,
    [property: Description("Fewest digits of the running number, which zeros fill in front, from 1 to 18.")]
    [property: Required]
    [property: Range(1, NumberFormat.MaxDigits)]
    int? Digits);
