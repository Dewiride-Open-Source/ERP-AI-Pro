using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application.Queries.GetParameterSet;

internal sealed record ShownParameterSet(ParameterSetDetails Set, ParameterVersionView? Shown);
