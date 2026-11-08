using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Domain;

public static class NumberingErrors
{
    public const string NextNumberField = "NextNumber";

    public const string DocumentTypeField = "DocumentType";

    public const string VersionField = "Version";

    public static readonly Error SeriesNotFound = Error.NotFound("numbering.series-not-found", "No numbering has this id.");

    public static readonly Error VersionInvalid = Error.Validation("numbering.version-invalid", "The version is not one the API gave.", Field(VersionField, "Send the version the API last returned."));

    public static Error UnknownDocument(string documentType) =>
        Error.Validation("numbering.document-unknown", $"No kind of document is numbered under the key {documentType}.", Field(DocumentTypeField, "Choose a kind of document the ERP numbers."));

    public static Error NotSetUp(NumberedDocument document) =>
        Error.Failure("numbering.not-set-up", $"{Title(document)} have no numbering yet; set it up under Document numbering first.");

    public static Error AlreadySetUp(NumberedDocument document) =>
        Error.Conflict("numbering.already-set-up", $"{Title(document)} already have a numbering; change it instead.");

    public static Error NoRule(NumberedDocument document, FinancialYear year) =>
        Error.Failure("numbering.no-rule", $"The ERP holds no rule for the numbers of {Lower(document)} in {year}, so it cannot number them.");

    public static Error FormatNotAllowed(NumberedDocument document, FinancialYear year) =>
        Error.Failure("numbering.format-not-allowed", $"The numbering of {Lower(document)} does not meet the rule for {year}; change it under Document numbering.");

    public static Error Exhausted(NumberedDocument document, NumberSeriesYear opened)
    {
        ArgumentNullException.ThrowIfNull(opened);

        return Error.Failure("numbering.exhausted", $"{Title(document)} have used every number of {opened.Year} that fits in {opened.MaxLength} characters.");
    }

    public static Error YearInUse(FinancialYear year) =>
        Error.Validation("numbering.year-in-use", $"{year} has given numbers, so its next number stays as it is.", Field(NextNumberField, $"{year} has given numbers, so its next number cannot change."));

    public static Error NextNumberOutOfRange(long lastNumber) =>
        Error.Validation("numbering.next-number-out-of-range", "The next number is outside the numbers this format can give.", Field(NextNumberField, $"Use a number from 1 to {lastNumber}."));

    public static Error FormatInvalid(IReadOnlyDictionary<string, string[]> problems) =>
        Error.Validation("numbering.format-invalid", "The numbering format is not allowed.", problems);

    public static Error Busy(NumberedDocument document) =>
        Error.Conflict(EfUnitOfWork.ConcurrencyConflictCode, $"Another save is numbering {Lower(document)} or changing their numbering; try again.");

    private static Dictionary<string, string[]> Field(string field, string message) => new(StringComparer.Ordinal) { [field] = [message] };

    private static string Title(NumberedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.Title;
    }

    private static string Lower(NumberedDocument document) => Title(document).ToLowerInvariant();
}
