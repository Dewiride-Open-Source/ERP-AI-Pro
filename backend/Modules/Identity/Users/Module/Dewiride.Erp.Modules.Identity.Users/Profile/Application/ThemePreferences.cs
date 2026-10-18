using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.Profile.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Application;

internal static class ThemePreferences
{
    public static ThemePreference ToPreference(UserTheme theme) => theme switch
    {
        UserTheme.System => ThemePreference.System,
        UserTheme.Light => ThemePreference.Light,
        UserTheme.Dark => ThemePreference.Dark,
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
    };

    public static UserTheme ToUserTheme(ThemePreference preference) => preference switch
    {
        ThemePreference.System => UserTheme.System,
        ThemePreference.Light => UserTheme.Light,
        ThemePreference.Dark => UserTheme.Dark,
        _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, null),
    };

    public static ChosenTheme Of(PersonTheme? chosen) =>
        chosen is null ? ChosenTheme.DeviceTheme : new ChosenTheme(ToPreference(chosen.Theme), chosen.ChoiceId);
}
