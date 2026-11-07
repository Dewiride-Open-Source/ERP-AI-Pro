using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;

public sealed record ParameterSetEntry(
    string Key,
    string Title,
    string Description,
    ParameterSetBasis Basis,
    ParameterAsOf AsOf,
    DateOnly FirstVersionFrom,
    int VersionCount,
    ParameterVersionEntry? Current,
    ParameterVersionEntry? Next,
    int EntryCount);
