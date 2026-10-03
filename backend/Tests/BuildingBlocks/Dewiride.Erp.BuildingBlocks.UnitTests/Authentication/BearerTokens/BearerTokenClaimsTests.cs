using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.BearerTokens;

public sealed class BearerTokenClaimsTests
{
    private const string ObjectId = "3f0c2a8e-6b1d-4c7e-9a52-0d8e4f1b7c31";

    [Fact]
    public void HolderOf_TokenWithScopes_IsAPerson()
    {
        Assert.Equal(BearerTokenHolder.Person, BearerTokenClaims.HolderOf(Token(("scp", "Erp.Read"), ("oid", ObjectId), ("sub", "pairwise"))));
    }

    [Fact]
    public void HolderOf_TokenWithScopesOfThePersonTokenType_IsAPerson()
    {
        Assert.Equal(BearerTokenHolder.Person, BearerTokenClaims.HolderOf(Token(("scp", "Erp.Read"), ("idtyp", "user"))));
    }

    [Fact]
    public void HolderOf_TokenWithScopesOfTheApplicationTokenType_HasNoHolder()
    {
        Assert.Null(BearerTokenClaims.HolderOf(Token(("scp", "Erp.Read"), ("idtyp", "app"), ("oid", ObjectId), ("sub", ObjectId))));
    }

    [Fact]
    public void HolderOf_TokenOfTheApplicationTokenType_IsAnApplication()
    {
        Assert.Equal(BearerTokenHolder.Application, BearerTokenClaims.HolderOf(Token(("idtyp", "app"), ("roles", "Erp.Integration"))));
    }

    [Fact]
    public void HolderOf_TokenWithoutATokenTypeWhoseSubjectIsItsObjectId_IsAnApplication()
    {
        Assert.Equal(BearerTokenHolder.Application, BearerTokenClaims.HolderOf(Token(("oid", ObjectId), ("sub", ObjectId), ("roles", "Erp.Integration"))));
    }

    [Fact]
    public void HolderOf_TokenWithoutScopesWhoseSubjectIsNotItsObjectId_HasNoHolder()
    {
        Assert.Null(BearerTokenClaims.HolderOf(Token(("oid", ObjectId), ("sub", "pairwise"), ("roles", "Erp.User"))));
    }

    [Theory]
    [InlineData("device")]
    [InlineData("user")]
    public void HolderOf_TokenWithoutScopesOfAnotherTokenType_HasNoHolder(string tokenType)
    {
        Assert.Null(BearerTokenClaims.HolderOf(Token(("idtyp", tokenType), ("oid", ObjectId), ("sub", ObjectId))));
    }

    [Fact]
    public void ScopesOf_TokenWithSpaceSeparatedScopes_ListsEachScope()
    {
        Assert.Equal(["Erp.Read", "Erp.Write"], BearerTokenClaims.ScopesOf(Token(("scp", " Erp.Read  Erp.Write "))));
    }

    [Fact]
    public void RolesOf_TokenWithRoleClaims_ListsEachRole()
    {
        Assert.Equal(["Erp.Integration", "Erp.Reports"], BearerTokenClaims.RolesOf(Token(("roles", "Erp.Integration"), ("roles", "Erp.Reports"))));
    }

    private static ClaimsPrincipal Token(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(claim => new Claim(claim.Type, claim.Value)), "Bearer"));
}
