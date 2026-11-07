using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application.Queries.ListParameterSets;

internal sealed record ListParameterSetsQuery : IQuery<IReadOnlyList<ParameterSetEntry>>;
