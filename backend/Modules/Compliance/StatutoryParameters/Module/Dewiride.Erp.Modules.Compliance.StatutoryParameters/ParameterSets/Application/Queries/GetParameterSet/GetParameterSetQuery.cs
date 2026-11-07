using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application.Queries.GetParameterSet;

internal sealed record GetParameterSetQuery(string Key, DateOnly? On) : IQuery<ShownParameterSet>;
