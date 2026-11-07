using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Endpoints.Requests;

public sealed record ListIndianStatesRequest(
    [property: FromQuery(Name = "on")]
    [property: Description("Date whose State codes are wanted, as yyyy-MM-dd; absent lists every code with the dates it applies to.")]
    DateOnly? On);
