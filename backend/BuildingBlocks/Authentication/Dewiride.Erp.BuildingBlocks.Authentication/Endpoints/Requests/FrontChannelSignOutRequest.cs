using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Requests;

public sealed record FrontChannelSignOutRequest(
    [property: FromQuery(Name = "iss")]
    [property: Description("Issuer of the Microsoft Entra ID session that was signed out, as the id tokens of that session name it.")]
    [property: Required]
    [property: StringLength(FrontChannelSignOutRequest.MaxIssuerLength)]
    string? Iss,
    [property: FromQuery(Name = "sid")]
    [property: Description("Id of the Microsoft Entra ID session that was signed out, the sid claim of the id tokens of that session.")]
    [property: Required]
    [property: StringLength(FrontChannelSignOutRequest.MaxSessionIdLength)]
    string? Sid)
{
    public const int MaxIssuerLength = 256;

    public const int MaxSessionIdLength = 128;
}
