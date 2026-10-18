using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Application;

internal sealed record ChosenTheme(ThemePreference Theme, Guid? ChoiceId)
{
    public static ChosenTheme DeviceTheme { get; } = new(ThemePreference.System, null);
}
