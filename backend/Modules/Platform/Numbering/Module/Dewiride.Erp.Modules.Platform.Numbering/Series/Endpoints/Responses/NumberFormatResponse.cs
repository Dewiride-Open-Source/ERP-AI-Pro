using Dewiride.Erp.BuildingBlocks.Numbering.Domain;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Responses;

/// <summary>How the numbers look: the fixed text, then the financial year and the separator when the year is shown, then the running number.</summary>
/// <param name="Prefix">Fixed text every number begins with; empty for none.</param>
/// <param name="Year">How the financial year is shown: none, full (2026-27) or abbreviated (26-27).</param>
/// <param name="Separator">Character between the financial year and the running number; empty for none.</param>
/// <param name="Digits">Fewest digits of the running number, which zeros fill in front.</param>
internal sealed record NumberFormatResponse(string Prefix, NumberYearStyle Year, string Separator, int Digits);
