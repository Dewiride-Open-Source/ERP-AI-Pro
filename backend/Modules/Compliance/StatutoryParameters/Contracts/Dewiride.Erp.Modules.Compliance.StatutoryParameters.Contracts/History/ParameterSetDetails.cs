using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;

public sealed record ParameterSetDetails(
    string Key,
    string Title,
    string Description,
    ParameterSetBasis Basis,
    ParameterAsOf AsOf,
    IReadOnlyList<ParameterColumnEntry> Columns,
    IReadOnlyList<ParameterVersionEntry> Versions,
    DateOnly? CurrentFrom);
