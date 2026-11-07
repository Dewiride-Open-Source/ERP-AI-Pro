using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Requests;

public sealed record GetParameterSetRequest(
    [property: FromRoute(Name = "key")]
    [property: Description("Key of the set, such as gst.central-tax-rates: two or more dot-separated parts of lower-case words joined by hyphens.")]
    [property: StringLength(ParameterSetLimits.KeyMaxLength)]
    [property: RegularExpression(ParameterSetLimits.KeyPattern)]
    string Key,
    [property: FromQuery(Name = "on")]
    [property: Description("Date whose version to show, as yyyy-MM-dd; absent shows the version in force today in India.")]
    DateOnly? On);
