using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Administration;

// A year's numbers as running numbers and as the text a document carries: the first, the next (null once every number that fits
// is given), the last that fits and the last given (null while the year has given none).
public sealed record NumberSeriesYearDetails(
    FinancialYear Year,
    NumberFormat Format,
    int MaxLength,
    long FirstNumber,
    long NextNumber,
    long LastNumber,
    bool InUse,
    string FirstText,
    string? NextText,
    string LastText,
    string? LastIssuedText)
{
    internal static NumberSeriesYearDetails Of(NumberSeriesYear year)
    {
        ArgumentNullException.ThrowIfNull(year);

        var last = year.LastNumber;
        return new NumberSeriesYearDetails(
            year.Year,
            year.Format,
            year.MaxLength,
            year.FirstNumber,
            year.NextNumber,
            last,
            year.InUse,
            year.Format.Render(year.Year, year.FirstNumber),
            year.NextNumber <= last ? year.Format.Render(year.Year, year.NextNumber) : null,
            year.Format.Render(year.Year, Math.Max(last, year.FirstNumber)),
            year.InUse ? year.Format.Render(year.Year, year.NextNumber - 1) : null);
    }
}
