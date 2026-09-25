using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Application.Queries.ListRecentStartups;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Endpoints.Requests;

public sealed record ListRecentStartupsRequest(
    [property: FromQuery(Name = "take")]
    [property: Description("How many starts to return, between 1 and 100; 20 when the caller does not ask.")]
    [property: Range(ListRecentStartupsQuery.MinimumCount, ListRecentStartupsQuery.MaximumCount)]
    int? Take)
{
    internal int Count => Take ?? ListRecentStartupsQuery.DefaultCount;
}
