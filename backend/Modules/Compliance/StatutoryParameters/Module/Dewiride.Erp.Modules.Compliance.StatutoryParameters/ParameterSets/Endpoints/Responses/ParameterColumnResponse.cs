using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;

/// <summary>One column of a set.</summary>
/// <param name="Name">Name of the column, which each entry's cells name.</param>
/// <param name="Title">Title of the column.</param>
/// <param name="Kind">What the column holds: text, a code, a whole number, a percent, an amount in rupees, one of its choices, a GST State code, a date or a day and month of the year.</param>
/// <param name="IsKey">Whether the column's value is the entry's key; exactly one column is.</param>
/// <param name="IsRowTitle">Whether the column titles an entry; when no column does, the key titles it.</param>
/// <param name="IsOptional">Whether an entry's cell of the column may be empty.</param>
/// <param name="Choices">The values a choice column holds, each with its title; empty for any other kind.</param>
internal sealed record ParameterColumnResponse(
    string Name,
    string Title,
    ParameterColumnKind Kind,
    bool IsKey,
    bool IsRowTitle,
    bool IsOptional,
    IReadOnlyList<ParameterChoiceResponse> Choices);
