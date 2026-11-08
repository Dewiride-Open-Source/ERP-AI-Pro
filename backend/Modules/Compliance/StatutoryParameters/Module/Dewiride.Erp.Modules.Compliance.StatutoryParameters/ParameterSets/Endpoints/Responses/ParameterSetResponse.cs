using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;

/// <summary>One statutory parameter set: its columns, its versions and the entries of the version in force on a date.</summary>
/// <param name="Key">Stable key of the set, such as gst.central-tax-rates.</param>
/// <param name="Title">Title of the set.</param>
/// <param name="Description">What the set holds and where it comes from.</param>
/// <param name="Basis">Whether the set is a government table (law) or the ERP's reading of one (interpretation).</param>
/// <param name="AsOf">The date the set answers for: the date of a supply, the first day of a tax period, the credit or payment at which tax is deducted at source, or the first day of a financial year.</param>
/// <param name="Columns">The set's columns, in the order its entries show them.</param>
/// <param name="Versions">Every version of the set, newest first.</param>
/// <param name="CurrentFrom">Date of the version in force today in India; null while the set's first version is still to come.</param>
/// <param name="Shown">The version in force on the date asked for, today in India when none was given; null when that date precedes the set's first version.</param>
internal sealed record ParameterSetResponse(
    string Key,
    string Title,
    string Description,
    ParameterSetBasis Basis,
    ParameterAsOf AsOf,
    IReadOnlyList<ParameterColumnResponse> Columns,
    IReadOnlyList<ParameterVersionSummaryResponse> Versions,
    DateOnly? CurrentFrom,
    ParameterVersionResponse? Shown);
