using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Application.Queries.ListIndianStates;

internal sealed record ListIndianStatesQuery(DateOnly? On) : IQuery<IReadOnlyList<IndianStateEntry>>;
