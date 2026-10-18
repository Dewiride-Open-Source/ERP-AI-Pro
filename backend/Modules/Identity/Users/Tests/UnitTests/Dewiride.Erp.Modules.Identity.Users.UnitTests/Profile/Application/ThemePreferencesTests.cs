using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Profile.Application;
using Dewiride.Erp.Modules.Identity.Users.Profile.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.UnitTests.Profile.Application;

public sealed class ThemePreferencesTests
{
    [Theory]
    [InlineData(ThemePreference.System, nameof(UserTheme.System))]
    [InlineData(ThemePreference.Light, nameof(UserTheme.Light))]
    [InlineData(ThemePreference.Dark, nameof(UserTheme.Dark))]
    public void ToUserThemeThenToPreference_EachTheme_AnswersTheSameTheme(ThemePreference preference, string themeName)
    {
        var theme = Enum.Parse<UserTheme>(themeName);

        Assert.Equal(theme, ThemePreferences.ToUserTheme(preference));
        Assert.Equal(preference, ThemePreferences.ToPreference(theme));
    }

    [Fact]
    public void ToUserTheme_AValueThatNamesNoTheme_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThemePreferences.ToUserTheme((ThemePreference)0));
    }

    [Fact]
    public void ToPreference_AValueThatNamesNoTheme_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThemePreferences.ToPreference((UserTheme)4));
    }

    [Fact]
    public void Of_PersonWithoutAThemeOfTheirOwn_FollowsTheThemeOfTheirDeviceWithNoChoice()
    {
        Assert.Equal(new ChosenTheme(ThemePreference.System, null), ThemePreferences.Of(null));
    }

    [Fact]
    public void Of_PersonWhoChoseATheme_AnswersItWithItsChoice()
    {
        var chosen = PersonTheme.ChooseFirst(UserId.Create(), UserTheme.Dark);

        Assert.Equal(new ChosenTheme(ThemePreference.Dark, chosen.ChoiceId), ThemePreferences.Of(chosen));
    }
}
