namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Responses;

/// <summary>One financial year of a numbering.</summary>
/// <param name="Year">The financial year, such as 2026-27.</param>
/// <param name="Format">How the year's numbers look, fixed once the year gives a number.</param>
/// <param name="MaxLength">Most characters the law allowed a number of the year when the year opened.</param>
/// <param name="FirstNumber">Running number of the year's first number.</param>
/// <param name="NextNumber">Running number the year gives next.</param>
/// <param name="LastNumber">Last running number whose number fits in MaxLength characters.</param>
/// <param name="InUse">Whether the year has given a number, which fixes its format and first number.</param>
/// <param name="FirstText">The year's first number as a document carries it.</param>
/// <param name="NextText">The number the year gives next; null once every number that fits is given.</param>
/// <param name="LastText">The last number that fits.</param>
/// <param name="LastIssuedText">The last number the year gave; null while it has given none.</param>
internal sealed record NumberSeriesYearResponse(
    string Year,
    NumberFormatResponse Format,
    int MaxLength,
    long FirstNumber,
    long NextNumber,
    long LastNumber,
    bool InUse,
    string FirstText,
    string? NextText,
    string LastText,
    string? LastIssuedText);
