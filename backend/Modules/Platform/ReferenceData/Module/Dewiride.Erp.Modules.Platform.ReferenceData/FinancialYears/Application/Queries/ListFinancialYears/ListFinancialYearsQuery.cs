using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Application.Queries.ListFinancialYears;

internal sealed record ListFinancialYearsQuery(string From, string To) : IQuery<IReadOnlyList<FinancialYear>>;
