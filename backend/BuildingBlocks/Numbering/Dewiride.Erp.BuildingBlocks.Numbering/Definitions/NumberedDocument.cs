namespace Dewiride.Erp.BuildingBlocks.Numbering.Definitions;

// The key names a kind of document in its numbering's row, so it never follows a class name: renaming one leaves its numbering
// under a key no release declares. The title names the documents as a person calls them ("Tax invoices"), and the serial-number
// rule is the key of the rule their numbers follow, which ISerialNumberRules answers for each financial year.
public sealed record NumberedDocument(string Key, string Title, string Description, string SerialNumberRule)
{
    public const int KeyMaxLength = NumberedDocumentKeys.MaxLength;

    public const int TitleMaxLength = 100;

    public const int DescriptionMaxLength = 400;

    public const int SerialNumberRuleMaxLength = 64;

    internal void EnsureValid()
    {
        if (!NumberedDocumentKeys.IsWellFormed(Key))
        {
            throw new ArgumentException($"The numbered document key '{Key}' must be three lower-case kebab segments, <domain>.<module>.<document>, of at most {KeyMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(Title) || Title.Length > TitleMaxLength)
        {
            throw new ArgumentException($"The numbered document {Key} needs a title of 1 to {TitleMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(Description) || Description.Length > DescriptionMaxLength)
        {
            throw new ArgumentException($"The numbered document {Key} needs a description of 1 to {DescriptionMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(SerialNumberRule) || SerialNumberRule.Length > SerialNumberRuleMaxLength)
        {
            throw new ArgumentException($"The numbered document {Key} needs a serial-number rule of 1 to {SerialNumberRuleMaxLength} characters.");
        }
    }
}
