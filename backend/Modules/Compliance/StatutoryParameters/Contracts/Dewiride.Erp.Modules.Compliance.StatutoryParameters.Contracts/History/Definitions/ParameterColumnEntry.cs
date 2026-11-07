namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;

public sealed record ParameterColumnEntry(
    string Name,
    string Title,
    ParameterColumnKind Kind,
    bool IsKey,
    bool IsRowTitle,
    bool IsOptional,
    IReadOnlyList<ParameterChoiceEntry> Choices);
