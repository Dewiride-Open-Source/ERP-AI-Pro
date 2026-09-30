using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Requests;

public sealed record LoginRequest(
    [property: FromQuery(Name = "returnUrl")]
    [property: Description("Local path of the page to open once signed in, such as /platform/attachments; the home page when omitted.")]
    [property: StringLength(LoginRequest.MaxReturnUrlLength)]
    string? ReturnUrl) : IValidatableObject
{
    public const int MaxReturnUrlLength = 2048;

    public const string DefaultReturnUrl = "/";

    public const string NotLocalMessage = "The return address must be a path on this site, such as /platform/attachments.";

    public string LocalReturnUrl => string.IsNullOrEmpty(ReturnUrl) ? DefaultReturnUrl : ReturnUrl;

    // IsLocalUrl also accepts the virtual path form ~/, which a browser would resolve against the current path. A browser
    // percent-encodes every address it sends, and the path ends up in a Location header, which carries visible ASCII only.
    public static bool IsLocalPath(string? returnUrl) =>
        string.IsNullOrEmpty(returnUrl)
        || (returnUrl[0] == '/' && RedirectHttpResult.IsLocalUrl(returnUrl) && !returnUrl.AsSpan().ContainsAnyExceptInRange('!', '~'));

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsLocalPath(ReturnUrl))
        {
            yield return new ValidationResult(NotLocalMessage, [nameof(ReturnUrl)]);
        }
    }
}
