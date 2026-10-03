using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.BearerTokens;

public sealed class BearerTokenAccessTests
{
    [Fact]
    public void Constructor_ScopesAndApplicationRoles_KeepsThemInOrder()
    {
        var access = new BearerTokenAccess(["Erp.Write", "Erp.Read"], ["Erp.Integration"]);

        Assert.Equal(["Erp.Write", "Erp.Read"], access.Scopes);
        Assert.Equal(["Erp.Integration"], access.ApplicationRoles);
    }

    [Fact]
    public void Constructor_NoScopeAndNoApplicationRole_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => new BearerTokenAccess([], []));

        Assert.StartsWith("A route that takes bearer tokens grants at least one scope or application role.", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Erp Read")]
    [InlineData("Erp.Read\t")]
    public void Constructor_ScopeOrRoleThatIsNotOneWord_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new BearerTokenAccess([name], []));
        Assert.Throws<ArgumentException>(() => new BearerTokenAccess(["Erp.Read"], [name]));
    }
}
