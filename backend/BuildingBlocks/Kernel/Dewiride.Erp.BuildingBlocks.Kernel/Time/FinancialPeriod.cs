using System;
using System.Globalization;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Time;

public readonly record struct FinancialPeriod
{
    public const int PeriodsInYear = 12;

    public FinancialPeriod(FinancialYear year, int number)
    {
        if (number is < 1 or > PeriodsInYear)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "A financial year has periods 1 to 12.");
        }

        Year = year;
        Number = number;
    }

    public FinancialYear Year { get; }

    public int Number { get; }

    public DateOnly Start => Year.Start.AddMonths(Number - 1);

    public DateOnly End => Start.AddMonths(1).AddDays(-1);

    public FinancialQuarter Quarter => new(Year, ((Number - 1) / 3) + 1);

    public string Label => Start.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public override string ToString() => Label;
}
