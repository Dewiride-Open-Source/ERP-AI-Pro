using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Domain;

// One financial year of a numbering, with the format and the longest number its year's rule allowed when it was opened. A number
// is taken in the transaction of the document that carries it, so a document that is not saved gives its number back and the
// year's numbers run from FirstNumber without a gap; once the year has given one, its format and first number never change.
[EntityTypeConfiguration(typeof(NumberSeriesYearConfiguration))]
public sealed class NumberSeriesYear : AggregateRoot<NumberSeriesYearId>, IAuditable, IVersioned
{
    public const long FirstOfYear = 1;

    private NumberSeriesYear(NumberSeriesYearId id)
        : base(id)
    {
    }

    private NumberSeriesYear(NumberSeriesYearId id, NumberSeriesId seriesId, FinancialYear year, NumberFormat format, int maxLength, long firstNumber)
        : base(id)
    {
        SeriesId = seriesId;
        Year = year;
        Format = format;
        MaxLength = maxLength;
        FirstNumber = firstNumber;
        NextNumber = firstNumber;
    }

    public NumberSeriesId SeriesId { get; private set; }

    public FinancialYear Year { get; private set; }

    public NumberFormat Format { get; private set; }

    public int MaxLength { get; private set; }

    public long FirstNumber { get; private set; }

    public long NextNumber { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool InUse => NextNumber > FirstNumber;

    public long LastNumber => Format.LastNumberWithin(MaxLength);

    internal static NumberSeriesYear Open(NumberSeriesId seriesId, FinancialYear year, NumberFormat format, int maxLength, long firstNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(firstNumber, FirstOfYear);

        return new NumberSeriesYear(NumberSeriesYearId.Create(), seriesId, year, format, maxLength, firstNumber);
    }

    internal bool TryIssue(out long number)
    {
        if (NextNumber > LastNumber)
        {
            number = 0;
            return false;
        }

        number = NextNumber;
        NextNumber++;

        return true;
    }

    internal void Reopen(NumberFormat format, int maxLength, long firstNumber)
    {
        if (InUse)
        {
            throw new InvalidOperationException($"{Year} has given numbers, so its format and first number stay as they are.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(firstNumber, FirstOfYear);

        Format = format;
        MaxLength = maxLength;
        FirstNumber = firstNumber;
        NextNumber = firstNumber;
    }
}
