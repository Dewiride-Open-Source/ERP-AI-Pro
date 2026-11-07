using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;

/// <summary>One statutory parameter set with the version in force today in India and the next one.</summary>
/// <param name="Key">Stable key of the set, such as gst.central-tax-rates.</param>
/// <param name="Title">Title of the set.</param>
/// <param name="Description">What the set holds and where it comes from.</param>
/// <param name="Basis">Whether the set is a government table (law) or the ERP's reading of one (interpretation).</param>
/// <param name="AsOf">The date the set answers for: the date of a supply, or the first day of a tax period.</param>
/// <param name="FirstVersionFrom">Date the set's first version applies from; the set holds nothing for an earlier date.</param>
/// <param name="VersionCount">Number of versions of the set.</param>
/// <param name="Current">The version in force today in India; null while the set's first version is still to come.</param>
/// <param name="Next">The first version after today; null when none is known.</param>
/// <param name="EntryCount">Number of entries in force today.</param>
internal sealed record ParameterSetSummaryResponse(
    string Key,
    string Title,
    string Description,
    ParameterSetBasis Basis,
    ParameterAsOf AsOf,
    DateOnly FirstVersionFrom,
    int VersionCount,
    ParameterVersionSummaryResponse? Current,
    ParameterVersionSummaryResponse? Next,
    int EntryCount);
