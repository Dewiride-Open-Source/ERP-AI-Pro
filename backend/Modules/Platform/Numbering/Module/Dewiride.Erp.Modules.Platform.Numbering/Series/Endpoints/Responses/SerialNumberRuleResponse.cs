namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Responses;

/// <summary>What the law allows the numbers of a kind of document in a financial year.</summary>
/// <param name="MaxLength">Most characters a number may have.</param>
/// <param name="SpecialCharacters">Characters a number may contain besides letters and digits, such as -/.</param>
internal sealed record SerialNumberRuleResponse(int MaxLength, string SpecialCharacters);
