using System;
using System.Globalization;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Time;

public readonly record struct FinancialQuarter
{
    public FinancialQuarter(FinancialYear year, int number)
    {
        if (number is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "A financial year has quarters 1 to 4.");
        }

        Year = year;
        Number = number;
    }

    public FinancialYear Year { get; }

    public int Number { get; }

    public DateOnly Start => Year.Start.AddMonths((Number - 1) * 3);

    public DateOnly End => Start.AddMonths(3).AddDays(-1);

    public string Label => string.Create(CultureInfo.InvariantCulture, $"Q{Number} {Year.Label}");

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public override string ToString() => Label;
}
