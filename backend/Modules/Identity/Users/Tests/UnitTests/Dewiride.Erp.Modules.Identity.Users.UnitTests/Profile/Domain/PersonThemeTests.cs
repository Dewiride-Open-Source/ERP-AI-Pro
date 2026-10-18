using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Profile.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.UnitTests.Profile.Domain;

public sealed class PersonThemeTests
{
    private static readonly UserId Person = new(new Guid("0199f3a4-5b6c-7d8e-9f01-23456789abcd"));

    [Theory]
    [InlineData(nameof(UserTheme.Light))]
    [InlineData(nameof(UserTheme.Dark))]
    public void ChooseFirst_AThemeOtherThanSystem_KeepsItForThePersonWithAChoiceOfItsOwn(string themeName)
    {
        var theme = Enum.Parse<UserTheme>(themeName);

        var chosen = PersonTheme.ChooseFirst(Person, theme);

        Assert.Equal(Person, chosen.Id);
        Assert.Equal(theme, chosen.Theme);
        Assert.NotEqual(Guid.Empty, chosen.ChoiceId);
    }

    [Fact]
    public void ChooseFirst_System_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PersonTheme.ChooseFirst(Person, UserTheme.System));
    }

    [Fact]
    public void ChooseFirst_SameThemeForTwoPeople_GivesBothTheSameChoice()
    {
        var first = PersonTheme.ChooseFirst(Person, UserTheme.Dark);
        var second = PersonTheme.ChooseFirst(UserId.Create(), UserTheme.Dark);

        Assert.Equal(first.ChoiceId, second.ChoiceId);
    }

    [Fact]
    public void Choose_AnotherTheme_KeepsItWithANewChoice()
    {
        var chosen = PersonTheme.ChooseFirst(Person, UserTheme.Dark);
        var before = chosen.ChoiceId;

        chosen.Choose(UserTheme.System);

        Assert.Equal(UserTheme.System, chosen.Theme);
        Assert.NotEqual(before, chosen.ChoiceId);
    }

    [Fact]
    public void Choose_TheThemeAlreadyHeld_KeepsTheChoice()
    {
        var chosen = PersonTheme.ChooseFirst(Person, UserTheme.Dark);
        var before = chosen.ChoiceId;

        chosen.Choose(UserTheme.Dark);

        Assert.Equal(UserTheme.Dark, chosen.Theme);
        Assert.Equal(before, chosen.ChoiceId);
    }

    [Fact]
    public void Choose_SameChangeFromTheSameChoice_GivesTheSameChoiceEachTime()
    {
        var first = PersonTheme.ChooseFirst(Person, UserTheme.Dark);
        var second = PersonTheme.ChooseFirst(Person, UserTheme.Dark);

        first.Choose(UserTheme.Light);
        second.Choose(UserTheme.Light);

        Assert.Equal(first.ChoiceId, second.ChoiceId);
    }

    [Fact]
    public void Choose_ThemeChosenAgainAfterAnother_GivesANewChoice()
    {
        var chosen = PersonTheme.ChooseFirst(Person, UserTheme.Dark);
        var darkFirst = chosen.ChoiceId;
        chosen.Choose(UserTheme.Light);

        chosen.Choose(UserTheme.Dark);

        Assert.NotEqual(darkFirst, chosen.ChoiceId);
    }

    [Fact]
    public void Choose_AnyChange_GivesAVersion8Uuid()
    {
        var chosen = PersonTheme.ChooseFirst(Person, UserTheme.Light);

        Assert.Equal(8, chosen.ChoiceId.Version);
        Assert.Equal(0b1000, chosen.ChoiceId.Variant & 0b1100);
    }

    [Fact]
    public void Choose_AValueThatNamesNoTheme_ThrowsAndKeepsTheTheme()
    {
        var chosen = PersonTheme.ChooseFirst(Person, UserTheme.Dark);
        var before = chosen.ChoiceId;

        Assert.Throws<ArgumentOutOfRangeException>(() => chosen.Choose((UserTheme)0));
        Assert.Equal(UserTheme.Dark, chosen.Theme);
        Assert.Equal(before, chosen.ChoiceId);
    }
}
