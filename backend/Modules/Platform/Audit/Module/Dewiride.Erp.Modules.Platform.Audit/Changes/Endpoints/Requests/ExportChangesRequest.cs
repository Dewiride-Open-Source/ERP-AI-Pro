using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Requests;

public sealed record ExportChangesRequest(
    [property: FromQuery(Name = "filter")]
    [property: Description("Semicolon-separated field:operator:value terms over the fields the list of changes allows; values are percent-encoded and an in-list separates its values with |. Absent, every change is exported.")]
    string? Filter);
