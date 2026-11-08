using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Requests;

public sealed record ChangeNumberSeriesRequest(
    [property: Description("How the numbers look: from the current financial year while it has given no number, otherwise from the next.")]
    [property: Required]
    NumberFormatRequest? Format,
    [property: Description("Running number the current financial year starts at, which changes only while the year has given no number; absent to keep it.")]
    long? NextNumber,
    [property: Description("Version of the numbering the change was made from, as the API last returned it.")]
    [property: Required]
    [property: StringLength(RecordVersion.InputMaxLength)]
    string? Version);
