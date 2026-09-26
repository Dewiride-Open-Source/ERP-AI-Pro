using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Requests;

public sealed record OpenContentRequest(
    [property: FromQuery(Name = "link")]
    [property: Description("Token of a download link created for this attachment by the person downloading it.")]
    [property: Required]
    [property: StringLength(64)]
    string? Link);
