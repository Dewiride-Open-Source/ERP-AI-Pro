using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Application.Queries.ListFinancialYears;

internal sealed class ListFinancialYearsHandler : IQueryHandler<ListFinancialYearsQuery, IReadOnlyList<FinancialYear>>
{
    public Task<Result<IReadOnlyList<FinancialYear>>> HandleAsync(ListFinancialYearsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Task.FromResult(List(query));
    }

    private static Result<IReadOnlyList<FinancialYear>> List(ListFinancialYearsQuery query)
    {
        if (!FinancialYear.TryParse(query.From, CultureInfo.InvariantCulture, out var first))
        {
            return FinancialYearErrors.LabelInvalid("from", query.From);
        }

        if (!FinancialYear.TryParse(query.To, CultureInfo.InvariantCulture, out var last))
        {
            return FinancialYearErrors.LabelInvalid("to", query.To);
        }

        if (last < first)
        {
            return FinancialYearErrors.RangeReversed;
        }

        if (last.StartYear - first.StartYear >= FinancialYearLimits.MaxYears)
        {
            return FinancialYearErrors.RangeTooLong;
        }

        var years = new List<FinancialYear>();
        for (var startYear = first.StartYear; startYear <= last.StartYear; startYear++)
        {
            years.Add(new FinancialYear(startYear));
        }

        return Result.Success<IReadOnlyList<FinancialYear>>(years);
    }
}
