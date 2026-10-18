using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Endpoints.Responses;

/// <summary>The colour theme the signed-in person's account holds once their choice is saved.</summary>
/// <param name="Theme">Colour theme the account holds.</param>
/// <param name="ChoiceId">Identifier of the account's choice of theme, which changes whenever the theme does; absent while the person follows system without having chosen another theme.</param>
internal sealed record ThemeChoiceResponse(ThemePreference Theme, Guid? ChoiceId);
