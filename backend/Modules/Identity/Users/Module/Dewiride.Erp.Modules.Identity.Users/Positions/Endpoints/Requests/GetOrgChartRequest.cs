using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Requests;

public sealed record GetOrgChartRequest(
    [property: FromQuery(Name = "on")]
    [property: Description("Date whose chart to read, in India; absent reads today's. Another date than today needs the permission identity.users.positions.read.")]
    DateOnly? On);
