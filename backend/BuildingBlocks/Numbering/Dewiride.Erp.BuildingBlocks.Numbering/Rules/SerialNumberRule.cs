namespace Dewiride.Erp.BuildingBlocks.Numbering.Rules;

// What the law of a financial year allows the numbers of a kind of document: at most MaxLength characters, each a letter, a
// numeral or one of SpecialCharacters.
public sealed record SerialNumberRule(int MaxLength, string SpecialCharacters);
