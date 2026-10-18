using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Endpoints.Requests;

public sealed record ChooseOwnThemeRequest(
    [property: Description("Colour theme the signed-in person chose for the web app: system follows the theme of their device.")]
    [property: EnumDataType(typeof(ThemePreference))]
    ThemePreference Theme);
