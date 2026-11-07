using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Application;

internal static class FinancialYearErrors
{
    public static readonly Error RangeReversed = Field("financial-year.range-reversed", "to", "The last financial year comes before the first.");

    public static readonly Error RangeTooLong = Field("financial-year.range-too-long", "to", $"At most {FinancialYearLimits.MaxYears} financial years are listed at once.");

    public static Error LabelInvalid(string member, string label) =>
        Field("financial-year.label-invalid", member, $"'{label}' is not the label of a financial year, such as 2026-27.");

    private static Error Field(string code, string member, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]>(StringComparer.Ordinal) { [member] = [message] });
}
