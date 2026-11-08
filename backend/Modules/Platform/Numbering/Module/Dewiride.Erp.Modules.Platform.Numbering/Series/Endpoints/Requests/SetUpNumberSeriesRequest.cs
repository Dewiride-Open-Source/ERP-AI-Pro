using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Requests;

public sealed record SetUpNumberSeriesRequest(
    [property: Description("Key of the kind of document to number, as the list of numbered documents gives it.")]
    [property: Required]
    [property: StringLength(NumberedDocument.KeyMaxLength)]
    string? DocumentType,
    [property: Description("How the numbers look.")]
    [property: Required]
    NumberFormatRequest? Format,
    [property: Description("Running number the current financial year starts at, from 1 to the last the format can give: 1, or the number after the last one given outside the ERP this year.")]
    [property: Required]
    long? NextNumber);
